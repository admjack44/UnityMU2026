namespace MUServer.Core.Models.Inventory
{
    public class InventoryItem
    {
        public byte Slot { get; set; }

        public byte ItemType { get; set; }

        public byte Width { get; set; } = 1;

        public byte Height { get; set; } = 1;
    }
}