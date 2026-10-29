namespace MessageClient;

public sealed class PublishTimeoutException(Exception innerException)
    : TimeoutException("Publishing exceeded its time budget. Delivery is uncertain.", innerException);
