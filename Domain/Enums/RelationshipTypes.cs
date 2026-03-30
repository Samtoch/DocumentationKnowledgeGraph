using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enums
{
    public static class RelationshipTypes
    {
        public const string Uses = "USES";
        public const string DependsOn = "DEPENDS_ON";
        public const string Implements = "IMPLEMENTS";
        public const string Contains = "CONTAINS";
        public const string PartOf = "PART_OF";
        public const string RelatedTo = "RELATED_TO";
        public const string References = "REFERENCES";
        public const string Mentions = "MENTIONS";
        public const string AuthoredBy = "AUTHORED_BY";
    }
}
