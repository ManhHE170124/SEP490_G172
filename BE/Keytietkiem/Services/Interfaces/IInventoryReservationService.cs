using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Interfaces
{
    public interface IInventoryReservationService
    {
        Task ReserveForOrderAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            IReadOnlyCollection<(Guid VariantId, int Quantity)> lines,
            DateTime nowUtc,
            DateTime reservedUntilUtc,
            CancellationToken ct = default);

        Task ExtendReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime newReservedUntilUtc,
            DateTime nowUtc,
            CancellationToken ct = default);

        Task ReleaseReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default);

        Task ReleaseExpiredReservationsAsync(
            KeytietkiemDbContext db,
            DateTime nowUtc,
            CancellationToken ct = default);

        Task FinalizeReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default);
    }
}
