using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AcquisitionAsset
{
    public long AcquisitionId { get; set; }

    public long AssetId { get; set; }

    public decimal? UnitCost { get; set; }

    public virtual Acquisition Acquisition { get; set; } = null!;

    public virtual Asset Asset { get; set; } = null!;
}
