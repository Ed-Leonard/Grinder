using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

public class LiveStreamMessage
{
    public int Id { get; set; }
    
    public int LiveStreamId { get; set; }

    public LiveStream LiveStream { get; set; } = null!;
    
    public int SenderId { get; set; }
    public User Sender { get; set; }
    
    [MaxLength(256)]
    public string Content { get; set; } = string.Empty;
    
    public DateTimeOffset SentAt { get; set; }
}