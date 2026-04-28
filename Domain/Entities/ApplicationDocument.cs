using System;

namespace SPRMS.API.Domain.Entities
{
    public class ApplicationDocument
    {
        public long DocumentID { get; set; }
        public long ApplicationID { get; set; }

        public string? DocumentType { get; set; }
        public string? FilePath { get; set; }

        public ScholarshipApplication? Application { get; set; }
    }
}
