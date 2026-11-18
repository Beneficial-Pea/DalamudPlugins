namespace Oracle.Services;

internal static class ReplayNativeRequest
{
    // UseAction's return alone cannot describe every native input-queue path.
    // Accept a newly created, exact action/target queue as a pending request,
    // even if no immediate execution was reported. Only a real ActionEffect
    // confirms use; pre-existing or unrelated input never establishes ownership.
    public static bool Accepted(bool returned, ReplayNativeWindow before, ReplayNativeWindow after,
        uint actionId, ulong targetId) => returned || (!before.ActionQueued && after.ActionQueued
        && after.QueuedActionType == 1 && after.QueuedActionId == actionId
        && after.QueuedTargetId == targetId && after.LastUsedSequence == before.LastUsedSequence);
}
