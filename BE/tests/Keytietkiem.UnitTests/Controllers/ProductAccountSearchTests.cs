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
    public class ProductAccountSearchTests
    {
        private readonly Mock<IProductAccountService> _serviceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly ProductAccountController _controller;

        public ProductAccountSearchTests()
        {
            _serviceMock = new Mock<IProductAccountService>();
            _auditLoggerMock = new Mock<IAuditLogger>();

            _controller = new ProductAccountController(
                _serviceMock.Object,
                _auditLoggerMock.Object);
        }

        // SPO01 – Keyword empty, không filter
        [Fact]
        public async Task SPO01_GetList_NoFilter_ReturnsFullList()
        {
            var filter = new ProductAccountFilterDto
            {
                SearchTerm = null,
                VariantId = null,
                ProductId = null,
                Status = null,
                ProductType = null,
                PageNumber = 1,
                PageSize = 20
            };

            var expectedResponse = new ProductAccountListResponseDto
            {
                CurrentPage = 1,
                TotalPages = 1,
                TotalCount = 3,
                Items = new List<ProductAccountListDto>
                {
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Product A",
                        VariantTitle = "Variant A",
                        AccountEmail = "a@example.com",
                        Status = "Active",
                        MaxUsers = 5,
                        CurrentUsers = 1,
                        SellPrice = 100
                    },
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Product B",
                        VariantTitle = "Variant B",
                        AccountEmail = "b@example.com",
                        Status = "Inactive",
                        MaxUsers = 3,
                        CurrentUsers = 0,
                        SellPrice = 50
                    },
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Product C",
                        VariantTitle = "Variant C",
                        AccountEmail = "c@example.com",
                        Status = "Active",
                        MaxUsers = 10,
                        CurrentUsers = 5,
                        SellPrice = 200
                    }
                }
            };

            _serviceMock
                .Setup(s => s.GetListAsync(
                    It.IsAny<ProductAccountFilterDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetList(filter);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountListResponseDto>(ok.Value);

            Assert.Equal(3, response.TotalCount);
            Assert.Equal(3, response.Items.Count);
            Assert.Equal(1, response.CurrentPage);

            _serviceMock.Verify(
                s => s.GetListAsync(
                    It.Is<ProductAccountFilterDto>(f =>
                        f.SearchTerm == null &&
                        f.Status == null &&
                        f.ProductType == null &&
                        f.PageNumber == 1 &&
                        f.PageSize == 20),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // SPO02 – Search theo keyword
        [Fact]
        public async Task SPO02_GetList_WithKeyword_ReturnsFilteredByKeyword()
        {
            var filter = new ProductAccountFilterDto
            {
                SearchTerm = "john",
                PageNumber = 1,
                PageSize = 20
            };

            var expectedResponse = new ProductAccountListResponseDto
            {
                CurrentPage = 1,
                TotalPages = 1,
                TotalCount = 1,
                Items = new List<ProductAccountListDto>
                {
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Shared Account",
                        VariantTitle = "Premium",
                        AccountEmail = "john.doe@example.com",
                        AccountUsername = "john.doe",
                        Status = "Active",
                        MaxUsers = 5,
                        CurrentUsers = 2,
                        SellPrice = 150
                    }
                }
            };

            _serviceMock
                .Setup(s => s.GetListAsync(
                    It.IsAny<ProductAccountFilterDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetList(filter);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountListResponseDto>(ok.Value);

            Assert.Single(response.Items);
            Assert.Equal("john.doe@example.com", response.Items[0].AccountEmail);

            _serviceMock.Verify(
                s => s.GetListAsync(
                    It.Is<ProductAccountFilterDto>(f =>
                        f.SearchTerm == "john" &&
                        f.PageNumber == 1 &&
                        f.PageSize == 20),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // SPO03 – Filter Status + ProductType
        [Fact]
        public async Task SPO03_GetList_WithStatusAndProductTypeFilter_ReturnsFilteredList()
        {
            var filter = new ProductAccountFilterDto
            {
                SearchTerm = null,
                Status = "Active",
                ProductType = "SHARED_ACCOUNT",
                PageNumber = 1,
                PageSize = 20
            };

            var expectedResponse = new ProductAccountListResponseDto
            {
                CurrentPage = 1,
                TotalPages = 1,
                TotalCount = 2,
                Items = new List<ProductAccountListDto>
                {
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Product Shared 1",
                        VariantTitle = "Pack 1",
                        AccountEmail = "shared1@example.com",
                        Status = "Active",
                        MaxUsers = 5,
                        CurrentUsers = 4,
                        SellPrice = 120
                    },
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Product Shared 2",
                        VariantTitle = "Pack 2",
                        AccountEmail = "shared2@example.com",
                        Status = "Active",
                        MaxUsers = 10,
                        CurrentUsers = 6,
                        SellPrice = 200
                    }
                }
            };

            _serviceMock
                .Setup(s => s.GetListAsync(
                    It.IsAny<ProductAccountFilterDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetList(filter);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountListResponseDto>(ok.Value);

            Assert.Equal(2, response.Items.Count);
            Assert.All(response.Items, i => Assert.Equal("Active", i.Status));

            _serviceMock.Verify(
                s => s.GetListAsync(
                    It.Is<ProductAccountFilterDto>(f =>
                        f.Status == "Active" &&
                        f.ProductType == "SHARED_ACCOUNT" &&
                        f.PageNumber == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // SPO04 – Không có record khớp (empty state)
        [Fact]
        public async Task SPO04_GetList_NoMatchingRecords_ReturnsEmptyList()
        {
            var filter = new ProductAccountFilterDto
            {
                SearchTerm = "not-exist",
                Status = "Inactive",
                PageNumber = 1,
                PageSize = 20
            };

            var expectedResponse = new ProductAccountListResponseDto
            {
                CurrentPage = 1,
                TotalPages = 0,
                TotalCount = 0,
                Items = new List<ProductAccountListDto>()
            };

            _serviceMock
                .Setup(s => s.GetListAsync(
                    It.IsAny<ProductAccountFilterDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetList(filter);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountListResponseDto>(ok.Value);

            Assert.Empty(response.Items);
            Assert.Equal(0, response.TotalCount);

            _serviceMock.Verify(
                s => s.GetListAsync(
                    It.Is<ProductAccountFilterDto>(f =>
                        f.SearchTerm == "not-exist" &&
                        f.Status == "Inactive"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // SPO05 – Phân trang: trang 2
        [Fact]
        public async Task SPO05_GetList_WithPaging_ReturnsSecondPage()
        {
            var filter = new ProductAccountFilterDto
            {
                PageNumber = 2,
                PageSize = 2
            };

            var expectedResponse = new ProductAccountListResponseDto
            {
                CurrentPage = 2,
                TotalPages = 3,
                TotalCount = 6,
                Items = new List<ProductAccountListDto>
                {
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Paged Product 3",
                        VariantTitle = "Variant 3",
                        AccountEmail = "p3@example.com",
                        Status = "Active",
                        MaxUsers = 5,
                        CurrentUsers = 1,
                        SellPrice = 90
                    },
                    new()
                    {
                        ProductAccountId = Guid.NewGuid(),
                        VariantId = Guid.NewGuid(),
                        ProductName = "Paged Product 4",
                        VariantTitle = "Variant 4",
                        AccountEmail = "p4@example.com",
                        Status = "Active",
                        MaxUsers = 5,
                        CurrentUsers = 2,
                        SellPrice = 95
                    }
                }
            };

            _serviceMock
                .Setup(s => s.GetListAsync(
                    It.IsAny<ProductAccountFilterDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetList(filter);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountListResponseDto>(ok.Value);

            Assert.Equal(2, response.CurrentPage);
            Assert.Equal(2, response.Items.Count);
            Assert.Equal(6, response.TotalCount);

            _serviceMock.Verify(
                s => s.GetListAsync(
                    It.Is<ProductAccountFilterDto>(f =>
                        f.PageNumber == 2 &&
                        f.PageSize == 2),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
