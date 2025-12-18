using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KlodTattooWeb.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider, ILogger logger)
        {
            using var scope = serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            try
            {
                logger.LogInformation("========================================");
                logger.LogInformation("🚀 INIZIO SEEDING DATABASE");
                logger.LogInformation("========================================");

                // ---------- DATABASE CREATION / MIGRATION ----------
                // Logic:
                // 1. MSSQL (Local): Use EnsureCreated() to bypass Postgres-specific migrations.
                // 2. Postgres/Other: Use Migrate() to keep consistent with production migrations.
                
                var provider = db.Database.ProviderName;
                if (provider != null && provider.Contains("SqlServer"))
                {
                    logger.LogInformation("🖥️ Rilevato MSSQL: Eseguo EnsureCreated()...");
                    await db.Database.EnsureCreatedAsync();
                    logger.LogInformation("✅ Database creato/verificato (EnsureCreated)");
                }
                else
                {
                    // Default behavior (Postgres / Production)
                    var pending = await db.Database.GetPendingMigrationsAsync();
                    if (pending.Any())
                    {
                        logger.LogWarning("⚠️ Migrazioni pendenti trovate. Le applico...");
                        await db.Database.MigrateAsync();
                        logger.LogInformation("✅ Migrazioni completate");
                    }
                    else
                    {
                        logger.LogInformation("✔️ Nessuna migrazione da applicare");
                    }
                }

                await Task.Delay(500); // sicurezza per Railway

                // ---------- RUOLI ----------
                string[] roles = { "Admin", "User" };
                logger.LogInformation("📌 Verifica ruoli di sistema...");

                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                        logger.LogInformation($"✔️ Creato ruolo '{role}'");
                    }
                }

                // ---------- ADMIN ----------
                logger.LogInformation("👤 Verifica utente Admin...");

                string adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
                string adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123!Strong";

                var admin = await userManager.FindByEmailAsync(adminEmail);

                if (admin == null)
                {
                    logger.LogWarning("⚠️ Admin non trovato. Creazione in corso...");

                    admin = new IdentityUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(admin, adminPassword);

                    if (!createResult.Succeeded)
                    {
                        logger.LogError("❌ ERRORE CREAZIONE ADMIN:");
                        logger.LogError(string.Join("\n", createResult.Errors.Select(e => e.Description)));
                        return;
                    }

                    await userManager.AddToRoleAsync(admin, "Admin");
                    logger.LogInformation("✔️ Admin creato e ruolo assegnato");
                }
                else
                {
                    logger.LogInformation($"✔️ Admin esistente trovato ({admin.Id})");

                    // Reset password forzato → previene NO LOGIN
                    var token = await userManager.GeneratePasswordResetTokenAsync(admin);
                    await userManager.ResetPasswordAsync(admin, token, adminPassword);
                    logger.LogInformation("✔️ Password Admin aggiornata");

                    if (!await userManager.IsInRoleAsync(admin, "Admin"))
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                        logger.LogInformation("✔️ Ruolo Admin riassegnato");
                    }
                }

                // ---------- TATTOO STYLES ----------
                logger.LogInformation("🎨 Seeding dei Tattoo Styles...");

                string[] styles = {
                    "Realistic",
                    "Fine line",
                    "Black Art",
                    "Lettering",
                    "Small Tattoos",
                    "Cartoons",
                    "Animals"
                };

                var existing = await db.TattooStyles.Select(s => s.Name).ToListAsync();
                int added = 0;

                foreach (var style in styles)
                {
                    if (!existing.Contains(style))
                    {
                        db.TattooStyles.Add(new TattooStyle { Name = style });
                        added++;
                        logger.LogInformation($"➕ Aggiunto stile: {style}");
                    }
                }

                if (added > 0)
                {
                    await db.SaveChangesAsync();
                    logger.LogInformation($"✔️ Salvati {added} nuovi stili");
                }
                else
                {
                    logger.LogInformation("✔️ Nessun nuovo stile da aggiungere");
                }

                logger.LogInformation("========================================");
                logger.LogInformation("🎉 SEEDING COMPLETATO");
                logger.LogInformation("========================================");
            }
            catch (Exception ex)
            {
                logger.LogError("❌ ERRORE DURANTE IL SEEDING");
                logger.LogError(ex.ToString());
            }
        }
    }
}
