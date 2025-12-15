using Keytietkiem.Constants;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Initialize role-permission data: permissions, modules, and admin role-permissions.
    /// </summary>
    public static class RolePermissionInitializer
    {
        public static async Task EnsureAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KeytietkiemDbContext>();
            var now = DateTime.UtcNow;

            await EnsurePermissionsAsync(db, now);
            var moduleMap = await EnsureModulesAsync(db, now);
            await EnsureAdminRolePermissionsAsync(db, moduleMap, now);
        }

        private static async Task EnsurePermissionsAsync(KeytietkiemDbContext db, DateTime now)
        {
            var existing = await db.Permissions.AsNoTracking()
                .Select(p => p.Code!.Trim().ToUpper())
                .ToListAsync();

            var toAdd = PermissionCodes.All
                .Where(code => !existing.Contains(code))
                .Select(code => new Permission
                {
                    Code = code,
                    PermissionName = code,
                    Description = code,
                    CreatedAt = now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                db.Permissions.AddRange(toAdd);
                await db.SaveChangesAsync();
            }
        }

        private static async Task<Dictionary<string, long>> EnsureModulesAsync(KeytietkiemDbContext db, DateTime now)
        {
            var desired = new (string Code, string Name, string? Description)[]
            {
                (ModuleCodes.ACCOUNT, "Account", "Account controller"),
                (ModuleCodes.AUDIT_LOG, "Audit Logs", "AuditLogs controller"),
                (ModuleCodes.BADGE, "Badge", "Badges controller"),
                (ModuleCodes.CATEGORY, "Category", "Categories controller"),
                (ModuleCodes.FAQ, "FAQ", "Faqs controller"),
                (ModuleCodes.LAYOUT_SECTION, "Layout Section", "LayoutSections controller"),
                (ModuleCodes.LICENSE_PACKAGE, "License Package", "LicensePackage controller"),
                (ModuleCodes.MODULE, "Module", "Modules controller"),
                (ModuleCodes.NOTIFICATION, "Notification", "Notifications controller"),
                (ModuleCodes.ORDER, "Order", "Orders controller"),
                (ModuleCodes.PAYMENT_GATEWAY, "Payment Gateway", "PaymentGateways controller"),
                (ModuleCodes.PAYMENT, "Payment", "Payments controller"),
                (ModuleCodes.PERMISSION, "Permission", "Permissions controller"),
                (ModuleCodes.POST, "Post", "Posts controller"),
                (ModuleCodes.POST_COMMENT, "Post Comment", "PostComments controller"),
                (ModuleCodes.POST_IMAGE, "Post Image", "PostImages controller"),
                (ModuleCodes.POST_TAG, "Post Tag", "Tags controller"),
                (ModuleCodes.POST_TYPE, "Post Type", "PostTypes operations in Posts controller"),
                (ModuleCodes.PRODUCT, "Product", "Products controller"),
                (ModuleCodes.PRODUCT_VARIANT, "Product Variant", "ProductVariants controller"),
                (ModuleCodes.PRODUCT_VARIANT_IMAGE, "Product Variant Image", "ProductVariantImages controller"),
                (ModuleCodes.PRODUCT_SECTION, "Product Section", "ProductSections controller"),
                (ModuleCodes.PRODUCT_ACCOUNT, "Product Account", "ProductAccount controller"),
                (ModuleCodes.PRODUCT_KEY, "Product Key", "ProductKey controller"),
                (ModuleCodes.PRODUCT_REPORT, "Product Report", "ProductReport controller"),
                (ModuleCodes.ROLE, "Role", "Roles controller"),
                (ModuleCodes.SLA_RULE_ADMIN, "SLA Rule", "SlaRulesAdmin controller"),
                (ModuleCodes.STOREFRONT_CART, "Storefront Cart", "StorefrontCart controller"),
                (ModuleCodes.STOREFRONT_HOMEPAGE, "Storefront Homepage", "StorefrontHomepage controller"),
                (ModuleCodes.STOREFRONT_PRODUCT, "Storefront Product", "StorefrontProducts controller"),
                (ModuleCodes.SUPPLIER, "Supplier", "Supplier controller"),
                (ModuleCodes.SUPPORT_CHAT, "Support Chat", "SupportChat controller"),
                (ModuleCodes.SUPPORT_DASHBOARD, "Support Dashboard", "SupportDashboardAdmin controller"),
                (ModuleCodes.SUPPORT_PLAN, "Support Plan", "SupportPlans controller"),
                (ModuleCodes.SUPPORT_PLAN_ADMIN, "Support Plan Admin", "SupportPlansAdmin controller"),
                (ModuleCodes.SUPPORT_PRIORITY_LOYALTY_RULE, "Support Priority Loyalty Rule", "SupportPriorityLoyaltyRules controller"),
                (ModuleCodes.TAG, "Tag", "Tags controller"),
                (ModuleCodes.TICKET, "Ticket", "Tickets controller"),
                (ModuleCodes.TICKET_REPLY, "Ticket Reply", "TicketReplies controller"),
                (ModuleCodes.TICKET_SUBJECT_TEMPLATE, "Ticket Subject Template", "TicketSubjectTemplatesAdmin controller"),
                (ModuleCodes.USER, "User", "Users controller"),
                (ModuleCodes.WEBSITE_SETTING, "Website Setting", "WebsiteSettings controller")
            };

            var existingCodes = await db.Modules.AsNoTracking()
                .Select(m => m.Code!.Trim().ToUpper())
                .ToListAsync();

            var toAdd = desired
                .Where(m => !existingCodes.Contains(m.Code))
                .Select(m => new Module
                {
                    Code = m.Code,
                    ModuleName = m.Name,
                    Description = m.Description,
                    CreatedAt = now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                db.Modules.AddRange(toAdd);
                await db.SaveChangesAsync();
            }

            return await db.Modules.AsNoTracking()
                .ToDictionaryAsync(m => m.Code!.Trim().ToUpper(), m => m.ModuleId);
        }

        private static async Task EnsureAdminRolePermissionsAsync(KeytietkiemDbContext db, Dictionary<string, long> moduleMap, DateTime now)
        {
            var admin = await db.Roles.FirstOrDefaultAsync(r => r.Code == RoleCodes.ADMIN);
            if (admin == null)
            {
                return;
            }

            var permissionMap = await db.Permissions.AsNoTracking()
                .ToDictionaryAsync(p => p.Code!.Trim().ToUpper(), p => p.PermissionId);

            var existing = await db.RolePermissions
                .Where(rp => rp.RoleId == admin.RoleId)
                .ToListAsync();

            var existingSet = new HashSet<(long ModuleId, long PermissionId)>(
                existing.Select(rp => (rp.ModuleId, rp.PermissionId)));

            var toAdd = new List<RolePermission>();

            foreach (var moduleCode in moduleMap.Keys)
            {
                var moduleId = moduleMap[moduleCode];
                foreach (var permCode in PermissionCodes.All)
                {
                    if (!permissionMap.TryGetValue(permCode, out var permId))
                        continue;

                    var key = (moduleId, permId);
                    if (!existingSet.Contains(key))
                    {
                        toAdd.Add(new RolePermission
                        {
                            RoleId = admin.RoleId,
                            ModuleId = moduleId,
                            PermissionId = permId,
                            IsActive = true
                        });
                    }
                    else
                    {
                        var rp = existing.First(e => e.ModuleId == moduleId && e.PermissionId == permId);
                        if (!rp.IsActive)
                        {
                            rp.IsActive = true;
                            db.RolePermissions.Update(rp);
                        }
                    }
                }
            }

            if (toAdd.Count > 0)
            {
                db.RolePermissions.AddRange(toAdd);
            }

            await db.SaveChangesAsync();
        }
    }
}



