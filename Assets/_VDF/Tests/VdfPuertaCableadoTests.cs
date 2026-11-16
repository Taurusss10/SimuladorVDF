using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 8: el cableado tiene que tener consecuencias.
    ///
    /// Recorriendo el catálogo de fallas en Play Mode salió que dos de las seis no producían
    /// ningún síntoma, porque el cableado solo decidía si el variador tenía tensión y todo lo
    /// demás se aplicaba igual estuviera conectado o no. Estos tests fijan lo contrario.
    /// </summary>
    public class VdfPuertaCableadoTests
    {
        private static VdfWiringGraph Correcto()
        {
            var g = new VdfWiringGraph();
            foreach (var red in VdfWiringSpec.Practica())
                for (int i = 1; i < red.Bornes.Length; i++)
                    g.Conectar(red.Bornes[0], red.Bornes[i]);
            return g;
        }

        [Test]
        public void ConElCableadoCorrecto_LlegaTodo()
        {
            var p = VdfPuertaCableado.Calcular(Correcto());

            Assert.IsTrue(p.Alimentado, "tensión");
            Assert.IsTrue(p.ComunDeEntradas, "común de entradas");
            Assert.IsTrue(p.S1 && p.S5 && p.S6, "órdenes de marcha y multi-velocidad");
            Assert.IsTrue(p.Motor, "las tres fases del motor");
            for (int n = 1; n <= 4; n++) Assert.IsTrue(p.Piloto(n), "piloto V" + n);
        }

        [Test]
        public void SinElComunSC_NoLlegaNingunaEntradaDigital()
        {
            // Es la falla SC_SIN_COMUN de la práctica 5. El síntoma que pide el enunciado es
            // «el variador no reacciona a ninguna de ellas», y antes el módulo daba
            // 10/25/35/50 Hz, igual que sano.
            var g = Correcto();
            g.Desconectar("GND24", "SC");

            var p = VdfPuertaCableado.Calcular(g);

            Assert.IsFalse(p.ComunDeEntradas);
            Assert.IsFalse(p.S1, "S1 necesita el retorno por SC");
            Assert.IsFalse(p.S5, "S5 necesita el retorno por SC");
            Assert.IsFalse(p.S6, "S6 necesita el retorno por SC");
            Assert.IsTrue(p.Alimentado, "pero el variador sigue teniendo tensión");
        }

        [Test]
        public void SinElCableDeUnPiloto_SoloSeApagaEsePiloto()
        {
            // Falla V3_SIN_CABLE. Lo importante es que los otros tres sigan encendiendo: si
            // se apagaran todos, el alumno no podría localizar cuál falta.
            var g = Correcto();
            g.Desconectar("PLC-Q0.2", "V3-A");

            var p = VdfPuertaCableado.Calcular(g);

            Assert.IsFalse(p.Piloto(3));
            Assert.IsTrue(p.Piloto(1) && p.Piloto(2) && p.Piloto(4));
        }

        [Test]
        public void SinElRetornoAL2_TampocoEnciendeElPiloto()
        {
            var g = Correcto();
            g.Desconectar("BRK-L2", "V2-B");

            Assert.IsFalse(VdfPuertaCableado.Calcular(g).Piloto(2),
                "Un piloto necesita las dos patas, no solo la salida del PLC.");
        }

        [Test]
        public void SinUnaFaseDelMotor_ElEjeNoGira()
        {
            var g = Correcto();
            g.Desconectar("V_MOT", "MOT-V");

            Assert.IsFalse(VdfPuertaCableado.Calcular(g).Motor);
        }

        [Test]
        public void SinGrafo_SeAsumeTodoCableado()
        {
            // Las prácticas 1, 2, 4 y 5 arrancan con el tablero ya cableado, y los tests de
            // lógica no montan grafo. Ninguno de los dos casos debe quedarse sin módulo.
            var p = VdfPuertaCableado.Calcular(null);

            Assert.IsTrue(p.Alimentado && p.S1 && p.S5 && p.S6 && p.Motor);
            for (int n = 1; n <= 4; n++) Assert.IsTrue(p.Piloto(n));
        }

        [Test]
        public void ElPilotoFueraDeRango_NoRevienta()
        {
            var p = VdfPuertaCableado.Calcular(Correcto());

            Assert.IsFalse(p.Piloto(0));
            Assert.IsFalse(p.Piloto(5));
        }

        [Test]
        public void LaPuertaFrenaLasSalidasDelPlc_PeroNoLaEmergencia()
        {
            var plc = new PlcFanLogic();
            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            vfd.AplicarConfiguracionDeReferencia();
            vfd.SetAlimentacion(true);

            plc.PulsarStart();
            plc.PulsarSubir();
            plc.Tick(4f);
            Assert.IsTrue(plc.SalidaS1, "el PLC sí manda la orden");

            var g = Correcto();
            g.Desconectar("GND24", "SC");
            var puerta = VdfPuertaCableado.Calcular(g);

            plc.AplicarA(vfd, puerta);
            Assert.IsFalse(vfd.GetTerminal(1), "pero al variador no le llega");

            // La emergencia corta por hardware, antes del PLC: tiene que funcionar incluso
            // con el tablero a medio cablear.
            plc.PulsarEmergencia();
            plc.AplicarA(vfd, puerta);
            Assert.AreEqual(VfdFault.ParadaEmergencia, vfd.Falla);
        }
    }
}
