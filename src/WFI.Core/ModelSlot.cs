namespace WFI.Core;

public sealed class ModelSlot
{
    public int Id {get;set;}
    public Guid? Token {get;set;}
    public DateTime? LeaseUntil {get;set;}
    public long Generation {get;set;}
}
