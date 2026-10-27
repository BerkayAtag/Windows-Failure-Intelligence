namespace WFI.Core;
public sealed record LearningExample(Guid ExecutionId,Guid DiagnosticId,string PlaybookCode,string Conclusion,string Verification,DateTime CompletedAt);
