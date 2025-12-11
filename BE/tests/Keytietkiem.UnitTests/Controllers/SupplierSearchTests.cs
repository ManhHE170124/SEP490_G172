

using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class SupplierSearchTests
    {
        private readonly SupplierController _controller;
        private readonly Mock<ISupplierService> _supplierServiceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;

        public SupplierSearchTests()
        {
            _supplierServiceMock = new Mock<ISupplierService>();
            _auditLoggerMock = new Mock<IAuditLogger>();
            _controller = new SupplierController(_supplierServiceMock.Object, _auditLoggerMock.Object);
        }

        private static PagedResult<SupplierListDto> CreatePagedResult(
            int pageNumber,
            int pageSize,
            int totalItems,
            IEnumerable<SupplierListDto>? items = null)
        {
            var list = items != null
                ? new List<SupplierListDto>(items)
                : new List<SupplierListDto>();

            // Constructor giống trong SupplierService:
            // new PagedResult<SupplierListDto>(suppliers, total, pageNumber, pageSize)
            return new PagedResult<SupplierListDto>(list, totalItems, pageNumber, pageSize);
        }

        private static SupplierListDto CreateSupplier(int id, string name, string status = "ACTIVE")
        {
            return new SupplierListDto
            {
                SupplierId = id,
                Name = name,
                Status = status,
                ContactEmail = $"{name.ToLower()}@example.com",
                ContactPhone = "0123456789",
                CreatedAt = DateTime.UtcNow,
                ActiveProductCount = 1
            };
        }

        // 1005S – Load suppliers first page, keyword empty, no status filter
        [Fact(DisplayName = "1005S - Load first page with empty keyword & no status filter")]
        public async Task TC_1005S_LoadFirstPage_NoFilters()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = null;
            string? keyword = null;

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 2,
                items: new[]
                {
                    CreateSupplier(1, "SupplierA"),
                    CreateSupplier(2, "SupplierB")
                });

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);

            // Controller trả đúng instance từ service
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 2005S – Search keyword, không filter status
        [Fact(DisplayName = "2005S - Search suppliers by keyword only")]
        public async Task TC_2005S_SearchByKeyword_Only()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = null;
            string? keyword = "gmail";

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 1,
                items: new[]
                {
                    CreateSupplier(3, "GmailSupplier")
                });

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 3005S – Filter theo status, không có keyword
        [Fact(DisplayName = "3005S - Filter suppliers by status only")]
        public async Task TC_3005S_FilterByStatus_Only()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = "INACTIVE";
            string? keyword = null;

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 1,
                items: new[]
                {
                    CreateSupplier(4, "InactiveSupplier", status: "INACTIVE")
                });

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 4005S – Keyword + status cùng lúc
        [Fact(DisplayName = "4005S - Search suppliers by keyword and status")]
        public async Task TC_4005S_SearchByKeyword_AndStatus()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = "ACTIVE";
            string? keyword = "vip";

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 1,
                items: new[]
                {
                    CreateSupplier(5, "VipSupplier", status: "ACTIVE")
                });

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 5005S – Phân trang: next page (pageNumber > 1)
        [Fact(DisplayName = "5005S - Load second page with paging")]
        public async Task TC_5005S_Pagination_SecondPage()
        {
            // Arrange
            var pageNumber = 2;
            var pageSize = 20;
            string? status = null;
            string? keyword = null;

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 30, // ví dụ total = 30, page 2 chứa 10 item
                items: new[]
                {
                    CreateSupplier(21, "Supplier21"),
                    CreateSupplier(22, "Supplier22")
                    // ... có thể thêm nếu muốn, không bắt buộc
                });

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 6005S – Không có kết quả (empty state)
        [Fact(DisplayName = "6005S - Search suppliers returns empty list")]
        public async Task TC_6005S_EmptyResult()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = "ACTIVE";
            string? keyword = "no-match";

            var expected = CreatePagedResult(
                pageNumber,
                pageSize,
                totalItems: 0,
                items: Array.Empty<SupplierListDto>());

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(expected));

            // Act
            var result = await _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<PagedResult<SupplierListDto>>(ok.Value);
            Assert.Same(expected, value);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // 7005S – Lỗi từ service (ví dụ lỗi DB) -> controller bubble exception (để middleware trả error)
        [Fact(DisplayName = "7005S - Service throws exception while loading suppliers")]
        public async Task TC_7005S_ServiceThrows_ErrorState()
        {
            // Arrange
            var pageNumber = 1;
            var pageSize = 20;
            string? status = null;
            string? keyword = null;

            var ex = new Exception("Database error");

            _supplierServiceMock
                .Setup(s => s.GetAllSuppliersAsync(
                    pageNumber,
                    pageSize,
                    status,
                    keyword,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<PagedResult<SupplierListDto>>(ex));

            // Act + Assert
            var thrown = await Assert.ThrowsAsync<Exception>(() =>
                _controller.GetAllSuppliers(pageNumber, pageSize, status, keyword));

            Assert.Same(ex, thrown);

            _supplierServiceMock.Verify(s => s.GetAllSuppliersAsync(
                pageNumber,
                pageSize,
                status,
                keyword,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
