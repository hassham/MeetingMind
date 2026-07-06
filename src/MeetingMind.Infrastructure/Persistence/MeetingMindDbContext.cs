using Microsoft.EntityFrameworkCore;

namespace MeetingMind.Infrastructure.Persistence
{
    public class MeetingMindDbContext : DbContext
    {
        public MeetingMindDbContext(DbContextOptions<MeetingMindDbContext> options)
        : base(options)
        {
        }
    }
}
