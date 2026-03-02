namespace ImplementadorCUAD.Models
{
    public class ColumnaConfiguracion
    {
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool Requerida { get; set; } = true;
        public List<string> Alias { get; set; } = new();
        public string TipoDato { get; set; } = string.Empty; // int, decimal, fecha, texto
        public int LargoMaximo { get; set; }
    }
}

