using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class AttorneyClient : BaseEntity
{
    public Guid AttorneyId { get; set; }
    public Attorney? Attorney { get; set; }

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
}
