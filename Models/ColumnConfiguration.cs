namespace ImplementadorCUAD.Models
{
    public class ColumnConfiguration
    {
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool Requerida { get; set; } = true;
        public List<string> Alias { get; set; } = new();
        public string TipoDato { get; set; } = string.Empty;
        public int LargoMaximo { get; set; }
    }
}

