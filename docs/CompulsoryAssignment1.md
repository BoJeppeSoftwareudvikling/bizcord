# Compulsory assignment 1 — Weeks 36–40

Our group built Bizcord’s Channel Service. It manages channels and records which message was posted most recently in each one. The other services in the architecture diagram are part of the system design. The week 40 test shows the part that runs end to end in this repository.

## Week 36 — What does Channel Service own?

We put channels, membership and channel permissions in one service. Messaging owns the messages themselves. In our design, Message Service asks Channel Service whether a user may post **before** accepting a message. Once a message has been posted, Channel Service can update its activity **afterwards** from an event. The permission check was a design proposal in week 36. The activity update became our week 40 feature.

**Where to look.** The project brief gives the product context. In the design document, read *Boundaries* for data ownership and *Interactions* alongside the container diagram for the calls between services. [Project brief](ProjectDescription.md) · [Boundaries and container diagram](DomainBoundariesAndServices.md)

## Week 37 — How does the service send and receive events?

We added RabbitMQ and a shared message client. Application code calls `IMessageClient` to publish an event or register a subscriber. The EasyNetQ adapter handles the RabbitMQ calls. Putting those calls in the adapter lets us test channel behaviour with a replacement client and keeps broker-specific code in one place.

**Where to look.** Compare the two methods on the interface with their implementation in the adapter. The package README shows how the application gets that implementation. [IMessageClient](../packages/MessageClient/src/IMessageClient.cs) · [EasyNetQ adapter](../packages/MessageClient/src/EasyNetQMessageClient.cs) · [Message client overview](../packages/MessageClient/README.md)

## Week 38 — What happens when someone creates a channel?

We exposed REST endpoints to create, list, read, edit and delete channels. For example, a client sends a name and an optional description to `POST /channels`. The application creates the channel, saves it and publishes a creation event. The client gets back the new ID and timestamps. Separate request and response types let clients use that API while membership and permission data stay inside the service.

**Where to look.** Start with `CreateChannelDto` to see what the caller supplies. Follow the POST action to `ChannelManagementService.CreateAsync` for the save and publish. Compare `ChannelDto` with `Channel` to see which fields the API exposes. [API types](../apps/channel-microservice/dtos/Channels/ChannelDtos.cs) · [REST controller](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Controllers/ChannelsController.cs) · [Application service](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs) · [Channel model](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Domain/Channel.cs)

## Week 39 — How do we run the service and its broker?

We gave Channel Service its own image and used Docker Compose to run it alongside RabbitMQ. The image builds from the repository root because the service references shared projects under `packages/`. Compose waits for the broker’s health check before starting Channel Service, which subscribes to events at startup.

**Where to look.** In the Dockerfile, follow the shared projects copied into the build stage. In Compose, look at `channel-microservice.build` and its `rabbitmq` dependency. The current stack also includes the later gateway, Vault and logging work. [Dockerfile](../apps/channel-microservice/Dockerfile) · [Compose services](../docker-compose.yml) · [Current startup instructions](../README.md#start)

## Week 40 — How do we know a posted message updates the right channel?

We chose channel activity as our first feature because it belongs to the channel and can be updated after Messaging accepts a post. On receiving `MessagePostedEvent`, Channel Service records the message ID and posting time and publishes an activity event. Keeping the original message ID lets another service trace the update back to the post.

**Where to look.** The handler passes the channel ID, message ID and time to `RecordMessageActivityAsync`. The integration test sends a message through a real RabbitMQ container, receives the outgoing event and checks the stored channel. Using a real broker here exercises the subscription and delivery as well as the update. [Message handler](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Messaging/MessagePostedHandler.cs) · [Activity update](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs) · [Integration test](../apps/channel-microservice/tests/Integration/MessagePostedFlowTests.cs)

The week 40 task specified an `IMessageHandler<MessagePostedEvent>` interface. We used the callback already accepted by `IMessageClient.SubscribeAsync`. For each message, that callback opens a service scope and calls the concrete handler. [Subscription code](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Messaging/MessagePostedSubscription.cs)

Run this feature test from the repository root with Docker running:

```bash
dotnet test Bizcord.slnx --filter 'FullyQualifiedName~MessagePosted_IsConsumed_AndActivityEventIsPublished'
```
