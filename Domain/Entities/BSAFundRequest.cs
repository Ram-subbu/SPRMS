using SPRMS.Domain.Enums;

namespace SPRMS.API.Domain.Entities;

public class BSAFundRequest : AuditEntity
{
    public long      FundRequestID        { get; set; }
    public long      BSAID                { get; set; }
    public long      RequestedByID        { get; set; }
    public string    Activity             { get; set; } = "";
    public decimal   AmountRequested      { get; set; }
    public decimal?  AmountApproved       { get; set; }
    public string?   BankAccountDetails   { get; set; }
    public string    ProposalFilePath     { get; set; } = "";
    public string    ParticipantsFilePath { get; set; } = "";
    public Status    Status               { get; set; } = Status.PendingFO;
    public BSAAssociation BSA             { get; set; } = null!;
}

