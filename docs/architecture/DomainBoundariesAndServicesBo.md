# Bizcord - Domain Boundaries and Services

## Identified microservices

- **Message Service**: sends, stores, edits, deletes, and retrieves messages.
- **Channel Service**: owns channels, memberships, and permission rules.
- **User Profile Service**: owns profile data and user-specific settings.
- **Engagement Service**: owns reactions, mentions, and notifications.

## Boundaries

- Each service owns its own data and database.
- Services communicate through APIs or events, not shared database access.
- The **Message Service** can ask the **Channel Service** whether a user may post in a channel.
- The **Engagement Service** can react to message events and read profile data when notifications or mentions need recipient information.

## Main interactions

- Client sends/reads messages through **Message Service**.
- Client browses channels through **Channel Service**.
- Client reads profile data through **User Profile Service**.
- **Message Service** publishes message events to the **Event Broker**.
- **Engagement Service** consumes message events and updates reactions, mentions, or notifications.

## Container diagram

![Bizcord Backend Container Diagram](<../../img/Container diagram Bo.png>)
