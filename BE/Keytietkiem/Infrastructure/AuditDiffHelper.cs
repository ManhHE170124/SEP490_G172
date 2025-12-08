using System.Text.Json;
using Keytietkiem.DTOs.AuditLogs;
namespace Keytietkiem.Infrastructure
{
    public static class AuditDiffHelper
    {
        public static List<AuditChangeDto> BuildDiff(string? beforeJson, string? afterJson)
        {
            var changes = new List<AuditChangeDto>();

            // Case: cả 2 đều null hoặc rỗng => không có gì để so
            if (string.IsNullOrWhiteSpace(beforeJson) && string.IsNullOrWhiteSpace(afterJson))
                return changes;

            JsonElement? before = null;
            JsonElement? after = null;

            if (!string.IsNullOrWhiteSpace(beforeJson))
            {
                before = JsonSerializer.Deserialize<JsonElement>(beforeJson);
            }

            if (!string.IsNullOrWhiteSpace(afterJson))
            {
                after = JsonSerializer.Deserialize<JsonElement>(afterJson);
            }

            DiffRecursive(before, after, "", changes);

            return changes;
        }

        private static void DiffRecursive(
            JsonElement? before,
            JsonElement? after,
            string path,
            List<AuditChangeDto> acc)
        {
            // 1. Một bên null, một bên có dữ liệu => cả object đó xem là thay đổi
            if (before is null && after is null)
                return;

            if (before is null || after is null)
            {
                acc.Add(new AuditChangeDto
                {
                    FieldPath = path.Trim('.'),
                    Before = before?.ToString(),
                    After = after?.ToString()
                });
                return;
            }

            var b = before.Value;
            var a = after.Value;

            // 2. Nếu không phải object (string/number/bool/array...) thì so sánh trực tiếp
            if (b.ValueKind != JsonValueKind.Object || a.ValueKind != JsonValueKind.Object)
            {
                if (!JsonElement.DeepEquals(b, a))
                {
                    acc.Add(new AuditChangeDto
                    {
                        FieldPath = path.Trim('.'),
                        Before = b.ToString(),
                        After = a.ToString()
                    });
                }
                return;
            }


            // 3. Cả hai đều là object => đi từng property
            var beforeProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            var afterProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

            var allKeys = new HashSet<string>(beforeProps.Keys);
            allKeys.UnionWith(afterProps.Keys);

            foreach (var key in allKeys)
            {
                beforeProps.TryGetValue(key, out var bv);
                afterProps.TryGetValue(key, out var av);

                var childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";

                // property chỉ có ở before (bị xoá)
                if (!beforeProps.ContainsKey(key))
                {
                    acc.Add(new AuditChangeDto
                    {
                        FieldPath = childPath,
                        Before = null,
                        After = av.ToString()
                    });
                    continue;
                }

                // property chỉ có ở after (mới thêm)
                if (!afterProps.ContainsKey(key))
                {
                    acc.Add(new AuditChangeDto
                    {
                        FieldPath = childPath,
                        Before = bv.ToString(),
                        After = null
                    });
                    continue;
                }

                // property có ở cả hai
                // Nếu là object => đệ quy
                if (bv.ValueKind == JsonValueKind.Object && av.ValueKind == JsonValueKind.Object)
                {
                    DiffRecursive(bv, av, childPath, acc);
                }
                else
                {
                    if (!JsonElement.DeepEquals(bv, av))
                    {
                        acc.Add(new AuditChangeDto
                        {
                            FieldPath = childPath,
                            Before = bv.ToString(),
                            After = av.ToString()
                        });
                    }
                }

            }
        }
    }
}
