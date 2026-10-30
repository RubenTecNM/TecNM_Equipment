namespace TecnmInventario.Api.Common.Domain;

/// <summary>
/// Longitudes máximas de las columnas varchar del esquema <c>inventario</c>
/// que se validan en los comandos. La fuente autoritativa es database.sql y
/// la BD real; un test compara estas constantes contra information_schema.
/// </summary>
public static class DbFieldLengths
{
    public static class Assets
    {
        public const int InventoryCode = 40;
        public const int SerialNumber = 150;
        public const int Hostname = 150;
    }

    public static class Roles
    {
        public const int Code = 40;
        public const int Name = 120;
    }

    public static class Users
    {
        public const int Username = 60;
        public const int Email = 254;
        public const int FirstName = 100;
        public const int LastName = 100;
    }

    public static class Acquisitions
    {
        public const int PurchaseOrder = 100;
        public const int InvoiceNumber = 100;
        public const int CurrencyCode = 3;
    }

    public static class Documents
    {
        public const int FileName = 255;
        public const int MimeType = 100;
    }

    public static class Suppliers
    {
        public const int Name = 180;
        public const int TaxId = 30;
        public const int ContactName = 150;
        public const int Email = 254;
        public const int Phone = 50;
    }

    public static class Warranties
    {
        public const int WarrantyNumber = 100;
    }
}
