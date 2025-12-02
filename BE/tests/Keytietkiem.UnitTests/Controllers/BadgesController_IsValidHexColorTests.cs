// File: tests/Keytietkiem.UnitTests/Controllers/BadgesController_IsValidHexColorTests.cs
using System;
using System.Reflection;
using Keytietkiem.Controllers;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit test WHITE-BOX cho method private static IsValidHexColor
    /// Mapping 1-1 với các UTC trong sheet IsValidHexColor.
    /// </summary>
    public class BadgesController_IsValidHexColorTests
    {
        /// <summary>
        /// Helper dùng reflection để gọi private static bool IsValidHexColor(string? color)
        /// </summary>
        private static bool InvokeIsValidHexColor(string? color)
        {
            var method = typeof(BadgesController)
                .GetMethod("IsValidHexColor",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method); // đảm bảo method tìm được

            var result = method!.Invoke(null, new object?[] { color });
            return (bool)result!;
        }

        // Sử dụng Theory + InlineData để map 9 test case
        // Mỗi dòng là 1 UTC trong file Excel.
        [Theory]
        // UTC01 – color = null
        [InlineData(null, false, "UTC01 - null input")]
        // UTC02 – color toàn space
        [InlineData("   ", false, "UTC02 - spaces only")]
        // UTC03 – không bắt đầu bằng '#'
        [InlineData("123456", false, "UTC03 - no # prefix")]
        // UTC04 – '#12' độ dài 3 (<4) – invalid boundary
        [InlineData("#12", false, "UTC04 - length 3 (<4)")]
        // UTC05 – '#1aF' length=4, toàn hex – valid boundary #RGB
        [InlineData("#1aF", true, "UTC05 - valid #RGB")]
        // UTC06 – '#1X3' length=4, có ký tự non-hex
        [InlineData("#1X3", false, "UTC06 - invalid hex char (len=4)")]
        // UTC07 – '#1A2b3C' length=7, toàn hex – valid boundary #RRGGBB
        [InlineData("#1A2b3C", true, "UTC07 - valid #RRGGBB")]
        // UTC08 – '#1G2H3I' length=7, có ký tự non-hex
        [InlineData("#1G2H3I", false, "UTC08 - invalid hex char (len=7)")]
        // UTC09 – '#1234567' length=8 (>7) – invalid boundary
        [InlineData("#1234567", false, "UTC09 - length 8 (>7)")]
        public void IsValidHexColor_WhiteBox_AccordingToDesign(
            string? input,
            bool expected,
            string utcId)
        {
            // Arrange: (input & expected đã lấy từ InlineData)

            // Act
            var actual = InvokeIsValidHexColor(input);

            // Assert
            Assert.Equal(
                expected,
                actual
            );
        }
    }
}
