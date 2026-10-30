using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class VAssetSummary
{
    public long? AssetId { get; set; }

    public string? InventoryCode { get; set; }

    public string? AssetType { get; set; }

    public string? AssetTypeName { get; set; }

    public string? SerialNumber { get; set; }

    public string? Manufacturer { get; set; }

    public string? ModelName { get; set; }

    public string? Status { get; set; }

    public string? StatusName { get; set; }

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public string? Hostname { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? Version { get; set; }
}
