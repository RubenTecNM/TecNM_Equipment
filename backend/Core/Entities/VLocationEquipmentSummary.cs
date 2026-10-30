using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class VLocationEquipmentSummary
{
    public long? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public string? LocationName { get; set; }

    public long? TotalAssets { get; set; }
}
