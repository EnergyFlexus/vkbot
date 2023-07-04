global using Microsoft.EntityFrameworkCore;
global using System.ComponentModel.DataAnnotations.Schema;
global using System.ComponentModel.DataAnnotations;

namespace VkLib
{
	public class TechInfo
	{
		[Key]
		public long id {get; set;} = 1;

		public string public_token {get; set;} = null!;
		public string help_message {get; set;} = null!;
	}
	public class Peer
	{
		[Key]
		public long peer_id {get; set;}

		public string? custom_token {get; set;} = null;

		public Peer() 
        {}

        public Peer(long peer_id) =>
            this.peer_id = peer_id;
	}
	public class TechUser
	{
		[Key]
		public long user_id {get; set;}

		public bool is_tech {get; set;} = false;
	}
    public class ChatUser
    {
        // primary key
        public long user_id {get; set;}
        public long peer_id {get; set;}

		public bool is_allowed {get; set;} = false;

        public ChatUser() 
        {}

        public ChatUser(long user_id, long peer_id) =>
            (this.user_id, this.peer_id) = (user_id, peer_id);
    }
    public class VkDbContext : DbContext
    {
        public DbSet<ChatUser> chat_users {get; set;} = null!;
		public DbSet<Peer> peers {get; set;} = null!;
		public DbSet<TechInfo> tech_info {get; set;} = null!;
		public DbSet<TechUser> tech_users {get; set;} = null!;
        
        public VkDbContext(DbContextOptions<VkDbContext> options):
            base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
			modelBuilder.Entity<ChatUser>().HasKey(u => new { u.user_id, u.peer_id});
        }
            
    }
}