using NUnit.Framework;
using VDF;

[SetUpFixture]
public class VdfModoMandoDeLosTests
{
    [OneTimeTearDown]
    public void Restaurar()
    {
        VdfSimulacion.ModoMandoForzado = null;
        VdfMando.Activo = VdfModoMando.Pulsadores;
    }
}
