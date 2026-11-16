namespace VDF
{
    public interface IVdfFuenteCableado
    {
        /// <summary>Qué está unido con qué.</summary>
        VdfWiringGraph Grafo { get; }

        /// <summary>Qué falta, qué sobra y si el cableado está correcto.</summary>
        VdfWiringReport Validar();

        /// <summary>Salta cada vez que se pone o se quita un cable.</summary>
        event System.Action CableadoCambiado;
    }
}
