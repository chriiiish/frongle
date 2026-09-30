using Microsoft.EntityFrameworkCore;

namespace Frongle.Api.Data;

public class FrongleDbContext(DbContextOptions<FrongleDbContext> options) : DbContext(options);
