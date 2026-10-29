namespace MessageClient;

public sealed class MessagingCapacityExceededException()
    : Exception("All publish slots are occupied.");
