using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime? UpdatedAt { get; protected set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        protected void UpdateModified()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
