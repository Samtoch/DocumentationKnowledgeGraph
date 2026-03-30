using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Infrastructure.SemanticKernel
{
    public class SemanticKernelEntityExtractor : IEntityExtractor
    {
        private readonly Kernel _kernel;
        private readonly ILogger<SemanticKernelEntityExtractor> _logger;
        private readonly string _extractionPrompt;

        public SemanticKernelEntityExtractor(
            Kernel kernel,
            ILogger<SemanticKernelEntityExtractor> logger)
        {
            _kernel = kernel;
            _logger = logger;
            _extractionPrompt = GetExtractionPrompt();
        }

        public async Task<ExtractionResult> ExtractAsync(string content, string source, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting entity extraction from {Source}, content length: {Length}",
                    source, content?.Length ?? 0);

                var prompt = $@"
{_extractionPrompt}

Documentation Source: {source}
Page Number: {pageNumber}

Content:
{content}

Remember to return ONLY valid JSON, no additional text or explanations.";

                var function = _kernel.CreateFunctionFromPrompt(prompt);
                var result = await _kernel.InvokeAsync(function);

                var jsonResult = result.GetValue<string>();
                if (string.IsNullOrWhiteSpace(jsonResult))
                {
                    return new ExtractionResult();
                }

                // Clean and parse JSON
                jsonResult = CleanJsonResponse(jsonResult);
                var extraction = await ParseExtractionResult(jsonResult, source);

                _logger.LogInformation("Extraction completed: {NodeCount} nodes, {RelCount} relationships",
                    extraction.Nodes.Count, extraction.Relationships.Count);

                return extraction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Entity extraction failed for {Source}", source);
                throw;
            }
        }

        public async Task<ExtractionResult> ExtractWithContextAsync(
            string content,
            string source,
            Dictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var contextJson = JsonSerializer.Serialize(context);

            var prompt = $@"
Additional context for extraction:
{contextJson}

{_extractionPrompt}

Documentation Source: {source}

Content:
{content}

Remember to return ONLY valid JSON, no additional text or explanations.";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);

            var jsonResult = result.GetValue<string>();
            return await ParseExtractionResult(jsonResult ?? "{}", source);
        }

        private string GetExtractionPrompt()
        {
            return @"You are a software documentation analysis expert. Analyze the following documentation and extract entities and relationships in JSON format.

Extract ALL entities from the content. An entity is any specific:
- Technology (e.g., .NET Core, React, PostgreSQL, Docker, Kubernetes)
- Programming language (e.g., C#, Python, JavaScript, TypeScript)
- Framework or library (e.g., Entity Framework, React, Express, Spring Boot)
- Tool or platform (e.g., Visual Studio, GitHub, Azure DevOps, Jira)
- Person (e.g., developers, architects, product managers mentioned by name)
- Project or module (e.g., Authentication Service, Payment Gateway, User Interface)
- API or endpoint (e.g., /api/users, GraphQL mutation, REST endpoint)
- Database (e.g., SQL Server, MongoDB, Redis, Elasticsearch)
- Concept or pattern (e.g., Microservices, Event-Driven Architecture, CQRS, SOLID)

For each entity, provide:
- name: The exact name as it appears (or canonical name)
- type: One of: TECHNOLOGY, PROGRAMMING_LANGUAGE, FRAMEWORK, LIBRARY, TOOL, PLATFORM, PERSON, PROJECT, MODULE, API, DATABASE, CONCEPT, DOCUMENT
- confidence: Float between 0 and 1 indicating how certain you are
- properties: Dictionary of additional attributes (version, vendor, description, etc.)
- aliases: List of alternative names or acronyms

For relationships, identify connections between entities:
- USES: When something uses a technology
- DEPENDS_ON: Dependency relationships
- IMPLEMENTS: When something implements a concept
- CONTAINS: Hierarchical relationships
- PART_OF: When something is part of something else
- RELATED_TO: General relationships
- REFERENCES: When documentation references another entity
- AUTHORED_BY: When content is authored by a person

Return the result as a valid JSON object with this structure:
{
  ""nodes"": [
    {
      ""id"": ""unique-id-here"",
      ""name"": ""Entity Name"",
      ""type"": ""TECHNOLOGY"",
      ""confidence"": 0.95,
      ""properties"": {
        ""version"": ""6.0"",
        ""vendor"": ""Microsoft""
      },
      ""aliases"": ["".NET"", ""DotNetCore""]
    }
  ],
  ""relationships"": [
    {
      ""from"": ""source-node-id"",
      ""to"": ""target-node-id"",
      ""type"": ""USES"",
      ""confidence"": 0.9,
      ""context"": ""The application uses Entity Framework Core""
    }
  ]
}";
        }

        private string CleanJsonResponse(string response)
        {
            // Remove markdown code block indicators
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```json\s*", "");
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```\s*", "");
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

        private Task<ExtractionResult> ParseExtractionResult(string jsonData, string source)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var parsed = JsonSerializer.Deserialize<ExtractionData>(jsonData, options);

            var result = new ExtractionResult
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
                }).ToList() ?? new List<ExtractedRelationship>(),
                Metadata = new ExtractionMetadata
                {
                    DocumentSource = source,
                    ProcessingTime = TimeSpan.Zero,
                    ModelVersion = "1.0",
                    ExtractedAt = DateTime.UtcNow
                }
            };

            return Task.FromResult(result);
        }

    }
}