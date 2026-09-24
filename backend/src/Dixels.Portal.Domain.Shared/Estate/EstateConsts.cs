namespace Dixels.Portal.Estate;

/* Limits shared by more than one aggregate; per-aggregate limits live in {Aggregate}Consts. */
public static class EstateConsts
{
    /* A booking series and a maintenance series are both capped at this many occurrences. */
    public const int MaxRecurrenceOccurrences = 200;
}
