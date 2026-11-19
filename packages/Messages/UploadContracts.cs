namespace Messages;

// One upload creates one file, quota reservation, attachment message and activity.
// Its SagaId also identifies each of those operation-specific resources.
public sealed record StartUpload(Guid SagaId, Guid UserId, Guid ChannelId, long Bytes, DateTimeOffset PostedAt)
{
    public bool IsValid => SagaId != Guid.Empty && UserId != Guid.Empty && ChannelId != Guid.Empty
        && Bytes > 0 && PostedAt != default && PostedAt.Offset == TimeSpan.Zero;
}

public enum UploadStep
{
    StageFile, PostMessage, RecordActivity, ReserveQuota, ActivateFile,
    ReleaseQuota, RetractActivity, RetractMessage, DeleteFile, Completed, Rejected
}

public sealed record FileCommand(StartUpload Upload, UploadStep Step);
public sealed record QuotaCommand(StartUpload Upload, UploadStep Step);
public sealed record AttachmentMessageCommand(StartUpload Upload, UploadStep Step);
public sealed record AttachmentActivityCommand(StartUpload Upload, UploadStep Step);

// A null rejection confirms the local transaction. Technical errors produce no
// business rejection: the coordinator retries the same command and resource ID.
public sealed record UploadStepResult(Guid SagaId, UploadStep Step, string? Rejection = null);
