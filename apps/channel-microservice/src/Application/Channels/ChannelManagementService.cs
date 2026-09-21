using ChannelService.Application.Abstractions;
using ChannelService.Domain;
using ChannelService.Dtos.Channels;
using ChannelService.Messaging;

namespace ChannelService.Application.Channels;

public sealed class ChannelManagementService(
    IChannelRepository channelRepository,
    IMessageClient messageClient)
{
    private readonly IChannelRepository _channelRepository =
        channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));

    private readonly IMessageClient _messageClient =
        messageClient ?? throw new ArgumentNullException(nameof(messageClient));

    public async Task<ChannelDto> CreateAsync(
        CreateChannelCommand command,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(command.Name);
        var description = NormalizeDescription(command.Description);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _channelRepository.AddAsync(channel, cancellationToken);

        await _messageClient.PublishAsync(
            new ChannelCreatedEventDto(
                channel.Id,
                channel.Name,
                channel.Description,
                channel.CreatedAt),
            cancellationToken);

        return ToDto(channel);
    }

    public async Task<ChannelDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ValidateId(id);

        var channel = await _channelRepository.GetAsync(id, cancellationToken);

        return channel is null ? null : ToDto(channel);
    }

    public async Task<IReadOnlyCollection<ChannelDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var channels = await _channelRepository.ListAsync(cancellationToken);

        return channels
            .Select(ToDto)
            .ToList();
    }

    public async Task<ChannelDto?> UpdateAsync(
        Guid id,
        UpdateChannelCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(id);

        var channel = await _channelRepository.GetAsync(id, cancellationToken);

        if (channel is null)
        {
            return null;
        }

        channel.Name = NormalizeName(command.Name);
        channel.Description = NormalizeDescription(command.Description);
        channel.UpdatedAt = DateTimeOffset.UtcNow;

        await _channelRepository.SaveAsync(channel, cancellationToken);

        await _messageClient.PublishAsync(
            new ChannelUpdatedEventDto(
                channel.Id,
                channel.Name,
                channel.Description,
                channel.UpdatedAt.Value),
            cancellationToken);

        return ToDto(channel);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ValidateId(id);

        var deleted = await _channelRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await _messageClient.PublishAsync(
            new ChannelDeletedEventDto(
                id,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return true;
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ChannelValidationException("Channel name is required.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > ChannelRules.MaxNameLength)
        {
            throw new ChannelValidationException(
                $"Channel name cannot be longer than {ChannelRules.MaxNameLength} characters.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > ChannelRules.MaxDescriptionLength)
        {
            throw new ChannelValidationException(
                $"Channel description cannot be longer than {ChannelRules.MaxDescriptionLength} characters.");
        }

        return trimmed;
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ChannelValidationException("Channel id cannot be empty.");
        }
    }

    private static ChannelDto ToDto(Channel channel)
    {
        return new ChannelDto(
            channel.Id,
            channel.Name,
            channel.Description,
            channel.CreatedAt,
            channel.UpdatedAt);
    }
}
