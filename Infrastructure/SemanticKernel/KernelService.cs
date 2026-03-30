using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
//using Neo4j.KernelMemory.MemoryStorage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Infrastructure.SemanticKernel
{
    public class KernelService
    {
        private readonly Kernel _kernel;
        private readonly ITextEmbeddingGenerationService _embeddingGenerator;
        private readonly ILogger<KernelService> _logger;
        private readonly string _extractionPrompt;
        private readonly IConfiguration _configuration;

        // Constants for extraction
        private const int MaxChunkSize = 8000; // Max tokens for LLM processing
        private const int MaxRetries = 3;
        private static readonly string[] SupportedEntityTypes = new[]
        {
            "TECHNOLOGY",
            "PROGRAMMING_LANGUAGE",
            "FRAMEWORK",
            "LIBRARY",
            "TOOL",
            "PLATFORM",
            "CONCEPT",
            "PERSON",
            "PROJECT",
            "API",
            "DATABASE"
        };

        private static readonly string[] SupportedRelationshipTypes = new[]
        {
            "USES",
            "DEPENDS_ON",
            "IMPLEMENTS",
            "CONTAINS",
            "PART_OF",
            "RELATED_TO",
            "REFERENCES",
            "MENTIONS",
            "AUTHORED_BY"
        };

        public KernelService(
            Kernel kernel,
            ITextEmbeddingGenerationService embeddingGenerator,
            IConfiguration configuration,
            ILogger<KernelService> logger)
        {
            _kernel = kernel;
            _embeddingGenerator = embeddingGenerator;
            _configuration = configuration;
            _logger = logger;
            _extractionPrompt = LoadExtractionPrompt();
        }

        /// <summary>
        /// Extract entities and relationships from document content
        /// </summary>
        public async Task<Domain.Entities.ExtractionResult> ExtractFromDocumentAsync(
            string content,
            string source,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting extraction from document source: {Source}, Page: {PageNumber}",
                    source, pageNumber);

                // Preprocess text (remove excessive whitespace, normalize line endings)
                var processedText = PreprocessText(content);

                // Extract information using LLM with retry logic
                var extractedData = await ExtractWithLLMWithRetry(processedText, source, cancellationToken);

                // Parse and create extraction result
                var extractionResult = ParseExtractedData(extractedData, source, pageNumber);

                // Validate extraction
                var validationResult = await ValidateExtractionAsync(extractionResult);
                if (validationResult.Warnings.Any())
                {
                    _logger.LogWarning("Extraction completed with warnings for {Source}: {Warnings}",
                        source, string.Join(", ", validationResult.Warnings));
                }

                // Generate embeddings for semantic search (if needed)
                await GenerateEmbeddingsAsync(extractionResult, cancellationToken);

                _logger.LogInformation("Successfully extracted {NodeCount} nodes and {RelCount} relationships from {Source}",
                    extractionResult.Nodes.Count, extractionResult.Relationships.Count, source);

                return extractionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting from document source: {Source}", source);
                throw new InvalidOperationException($"Failed to extract from document: {source}", ex);
            }
        }

        /// <summary>
        /// Extract from document content with additional context
        /// </summary>
        public async Task<Domain.Entities.ExtractionResult> ExtractWithContextAsync(
            string content,
            string source,
            Dictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting contextual extraction from source: {Source}", source);

                var processedText = PreprocessText(content);
                var contextJson = JsonSerializer.Serialize(context);

                var prompt = $@"
                Additional context for extraction:
                {contextJson}

                {processedText}";

                var extractedData = await ExtractWithLLMWithRetry(prompt, source, cancellationToken);
                var extractionResult = ParseExtractedData(extractedData, source, 1);

                return extractionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in contextual extraction from source: {Source}", source);
                throw;
            }
        }

        /// <summary>
        /// Reprocess an existing extraction with new content
        /// </summary>
        public async Task<Domain.Entities.ExtractionResult> ReprocessDocumentAsync(
            string content,
            string source,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Reprocessing document: {Source}, Page: {PageNumber}", source, pageNumber);

                // Extract fresh data
                var updatedExtraction = await ExtractFromDocumentAsync(content, source, pageNumber, cancellationToken);

                _logger.LogInformation("Successfully reprocessed document: {Source}", source);

                return updatedExtraction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reprocessing document: {Source}", source);
                throw;
            }
        }

        /// <summary>
        /// Validate extraction result
        /// </summary>
        public async Task<Domain.Entities.ExtractionValidationResult> ValidateExtractionAsync( 
            Domain.Entities.ExtractionResult extraction,
            CancellationToken cancellationToken = default)
        {
            var result = new ExtractionValidationResult
            {
                IsValid = true,
                Warnings = new List<string>(),
                Metadata = new Dictionary<string, object>
                {
                    ["validation_timestamp"] = DateTime.UtcNow,
                    ["node_count"] = extraction.Nodes.Count,
                    ["relationship_count"] = extraction.Relationships.Count
                }
            };

            // Validate nodes
            if (!extraction.Nodes.Any())
            {
                result.Errors.Add("No nodes were extracted");
                result.IsValid = false;
            }
            else
            {
                // Check for duplicate node names
                var nodeNames = extraction.Nodes.Select(n => n.Name.ToLowerInvariant()).ToList();
                var duplicateNodes = nodeNames.GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateNodes.Any())
                {
                    result.Warnings.Add($"Duplicate node names detected: {string.Join(", ", duplicateNodes)}");
                }

                // Check node types against supported types
                var invalidTypes = extraction.Nodes
                    .Select(n => n.Type)
                    .Where(t => !SupportedEntityTypes.Contains(t))
                    .ToList();

                if (invalidTypes.Any())
                {
                    result.Warnings.Add($"Unsupported node types detected: {string.Join(", ", invalidTypes.Distinct())}");
                }

                // Check confidence scores
                var lowConfidenceNodes = extraction.Nodes.Where(n => n.Confidence < 0.6).ToList();
                if (lowConfidenceNodes.Any())
                {
                    result.Warnings.Add($"{lowConfidenceNodes.Count} nodes have low confidence scores");
                }
            }

            // Validate relationships
            if (extraction.Relationships.Any())
            {
                // Check if all relationship nodes exist
                var nodeIds = extraction.Nodes.Select(n => n.Id).ToHashSet();
                var invalidRelationships = extraction.Relationships
                    .Where(r => !nodeIds.Contains(r.FromNodeId) || !nodeIds.Contains(r.ToNodeId))
                    .ToList();

                if (invalidRelationships.Any())
                {
                    result.Errors.Add($"{invalidRelationships.Count} relationships reference non-existent nodes");
                    result.IsValid = false;
                }

                // Check relationship types against supported types
                var invalidRelTypes = extraction.Relationships
                    .Select(r => r.Type)
                    .Where(t => !SupportedRelationshipTypes.Contains(t))
                    .ToList();

                if (invalidRelTypes.Any())
                {
                    result.Warnings.Add($"Unsupported relationship types detected: {string.Join(", ", invalidRelTypes.Distinct())}");
                }
            }

            // Calculate confidence score
            var confidenceScore = CalculateConfidenceScore(extraction);
            result.Metadata["confidence_score"] = confidenceScore;

            if (confidenceScore < 0.7)
            {
                result.Warnings.Add($"Low confidence score: {confidenceScore:P}");
            }

            return result;
        }

        #region Private Methods

        private string LoadExtractionPrompt()
        {
            try
            {
                var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "extraction_prompt.txt");
                if (File.Exists(promptPath))
                {
                    return File.ReadAllText(promptPath);
                }
            }
            catch
            {
                // Fallback to embedded prompt if file not found
            }

            return GetEmbeddedExtractionPrompt();
        }

        private string GetEmbeddedExtractionPrompt()
        {
            return @"You are a software documentation analysis expert. Analyze the following documentation and extract entities and relationships in JSON format.

Answer the following questions using information exclusively from this documentation:

1. What TECHNOLOGIES are mentioned? (languages, frameworks, libraries, tools, platforms)
2. What CONCEPTS or architectural patterns are discussed?
3. What PEOPLE (authors, contributors, roles) are mentioned?
4. What PROJECTS, modules, or components are referenced?
5. What APIs or endpoints are described?
6. What DATABASES or data stores are mentioned?

For each entity, identify:
- Name: The exact name as it appears
- Type: One of the supported types
- Confidence: How certain you are (0.0 to 1.0)

Identify relationships between entities such as:
- USES: When something uses a technology
- DEPENDS_ON: Dependency relationships
- IMPLEMENTS: When something implements a concept
- CONTAINS: Hierarchical relationships
- REFERENCES: When documentation references another entity
- AUTHORED_BY: When content is authored by a person

Provide your answer as a valid JSON document with this exact structure:
{
  ""nodes"": [
    {
      ""id"": ""unique-id"",
      ""name"": ""Entity Name"",
      ""type"": ""TECHNOLOGY"",
      ""confidence"": 0.95,
      ""properties"": {
        ""description"": ""Brief description"",
        ""version"": ""1.0"",
        ""source"": ""documentation""
      },
      ""aliases"": [""alt-name1"", ""alt-name2""]
    }
  ],
  ""relationships"": [
    {
      ""from"": ""source-node-id"",
      ""to"": ""target-node-id"",
      ""type"": ""USES"",
      ""confidence"": 0.9,
      ""context"": ""The application uses Entity Framework Core for data access""
    }
  ]
}

Only include entities and relationships that are explicitly mentioned or strongly implied in the content.";
        }

        private string PreprocessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove excessive whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            // Normalize line endings
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove any null characters
            text = text.Replace("\0", string.Empty);

            // Remove any non-printable characters
            text = new string(text.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray());

            return text.Trim();
        }

        private async Task<string> ExtractWithLLMWithRetry(
            string documentText,
            string source,
            CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    // Handle large documents by taking relevant portions
                    var textToProcess = documentText.Length > MaxChunkSize
                        ? documentText[..MaxChunkSize] + "... [truncated]"
                        : documentText;

                    var prompt = $@"
                    {_extractionPrompt}

                    Documentation Source: {source}

                    Documentation Content:
                    {textToProcess}

                    Remember to return ONLY valid JSON, no additional text or explanations.";

                    var function = _kernel.CreateFunctionFromPrompt(prompt);
                    var result = await _kernel.InvokeAsync(function);

                    var response = result.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        throw new InvalidOperationException("Empty response from LLM");
                    }

                    // Clean and validate JSON response
                    response = CleanJsonResponse(response);

                    // Validate JSON structure
                    //using JsonDocument.Parse(response);

                    return response;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid JSON response from LLM on attempt {Attempt} for {Source}", attempt, source);
                    if (attempt == MaxRetries)
                        throw new InvalidOperationException("Failed to get valid JSON from LLM after multiple attempts", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in LLM extraction on attempt {Attempt} for {Source}", attempt, source);
                    if (attempt == MaxRetries)
                        throw;

                    await Task.Delay(1000 * attempt, cancellationToken); // Exponential backoff
                }
            }

            throw new InvalidOperationException($"Failed to extract document information after {MaxRetries} attempts");
        }

        private string CleanJsonResponse(string response)
        {
            // Remove any markdown code block indicators
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```json\s*", "");
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```\s*", "");

            // Trim whitespace
            response = response.Trim();

            // Find the first { and last } to extract JSON object
            var startIndex = response.IndexOf('{');
            var endIndex = response.LastIndexOf('}');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                response = response[startIndex..(endIndex + 1)];
            }

            return response;
        }

        private Domain.Entities.ExtractionResult ParseExtractedData(string jsonData, string source, int pageNumber)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var parsed = JsonSerializer.Deserialize<ExtractionData>(jsonData, options);

                var result = new Domain.Entities.ExtractionResult
                {
                    Nodes = parsed?.Nodes?.Select(n => new ExtractedNode
                    {
                        Id = n.Id ?? Guid.NewGuid().ToString(),
                        Name = n.Name ?? "Unknown",
                        Type = n.Type ?? "UNKNOWN",
                        Confidence = n.Confidence,
                        Source = source,
                        Properties = n.Properties ?? new Dictionary<string, object>(),
                        Aliases = n.Aliases ?? new List<string>(),
                        ExtractedAt = DateTime.UtcNow
                    }).ToList() ?? new List<ExtractedNode>(),
                    Relationships = parsed?.Relationships?.Select(r => new ExtractedRelationship
                    {
                        Id = Guid.NewGuid().ToString(),
                        FromNodeId = r.From,
                        ToNodeId = r.To,
                        Type = r.Type ?? "RELATED_TO",
                        Confidence = r.Confidence,
                        Context = r.Context ?? string.Empty,
                        Properties = r.Properties ?? new Dictionary<string, object>(),
                        ExtractedAt = DateTime.UtcNow
                    }).ToList() ?? new List<ExtractedRelationship>()
                };

                // Add metadata
                result.Metadata = new Domain.Entities.ExtractionMetadata
                {
                    DocumentSource = source,
                    PageNumber = pageNumber,
                    ProcessingTime = TimeSpan.Zero, // Will be set by caller
                    ModelVersion = "1.0",
                    ExtractedAt = DateTime.UtcNow,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["source"] = source,
                        ["page"] = pageNumber
                    }
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing extracted data: {JsonData}", jsonData);
                throw new InvalidOperationException("Failed to parse extraction result", ex);
            }
        }

        private async Task GenerateEmbeddingsAsync(Domain.Entities.ExtractionResult extraction, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var node in extraction.Nodes)
                {
                    // Generate embedding for node name and description
                    var textToEmbed = $"{node.Name} {string.Join(" ", node.Aliases ?? new List<string>())}";
                    if (node.Properties.TryGetValue("description", out var desc))
                    {
                        textToEmbed += $" {desc}";
                    }

                    var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(textToEmbed);

                    // Store embedding in node properties for later use
                    node.Properties["embedding"] = embedding.ToArray();
                }

                _logger.LogDebug("Generated embeddings for {Count} nodes", extraction.Nodes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embeddings for some nodes");
            }
        }

        private double CalculateConfidenceScore(Domain.Entities.ExtractionResult extraction)
        {
            if (!extraction.Nodes.Any() && !extraction.Relationships.Any())
                return 0.0;

            var nodeConfidence = extraction.Nodes.Any()
                ? extraction.Nodes.Average(n => n.Confidence)
                : 0.0;

            var relConfidence = extraction.Relationships.Any()
                ? extraction.Relationships.Average(r => r.Confidence)
                : 0.0;

            var nodeWeight = 0.6; // Nodes are more important
            var relWeight = 0.4;   // Relationships add context

            return (nodeConfidence * nodeWeight) + (relConfidence * relWeight);
        }

        #endregion
    }

}
