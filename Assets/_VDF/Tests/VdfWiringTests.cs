using System.Collections.Generic;
using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// E4: el grafo de cableado y su validación son C# puro, así que se prueban enteros
    /// en EditMode sin escena, sin ratón y sin Play Mode.
    /// </summary>
    public class VdfWiringTests
    {
        private VdfWiringGraph _grafo;

        [SetUp]
        public void SetUp()
        {
            _grafo = new VdfWiringGraph();
        }

        // =================================================================================
        // Grafo
        // =================================================================================

        [Test]
        public void Conectar_EsSimetricoYNoDuplica()
        {
            Assert.IsTrue(_grafo.Conectar("A", "B"));
            Assert.IsFalse(_grafo.Conectar("A", "B"), "no debe admitir el mismo cable dos veces");
            Assert.IsFalse(_grafo.Conectar("B", "A"), "ni el mismo cable al reves");
            Assert.AreEqual(1, _grafo.Cuenta);
            Assert.IsTrue(_grafo.Existe("B", "A"));
        }

        [Test]
        public void Conectar_RechazaBuclesSobreElMismoBorne()
        {
            Assert.IsFalse(_grafo.Conectar("A", "A"));
            Assert.AreEqual(0, _grafo.Cuenta);
        }

        [Test]
        public void Desconectar_QuitaElCableEnCualquierOrden()
        {
            _grafo.Conectar("A", "B");
            Assert.IsTrue(_grafo.Desconectar("B", "A"));
            Assert.AreEqual(0, _grafo.Cuenta);
        }

        [Test]
        public void DesconectarTodoDe_QuitaSoloLosCablesDeEseBorne()
        {
            _grafo.Conectar("A", "B");
            _grafo.Conectar("A", "C");
            _grafo.Conectar("B", "C");

            Assert.AreEqual(2, _grafo.DesconectarTodoDe("A"));
            Assert.AreEqual(1, _grafo.Cuenta);
            Assert.IsTrue(_grafo.Existe("B", "C"));
        }

        [Test]
        public void MismaRed_SigueLaCadenaAunqueNoHayaCableDirecto()
        {
            _grafo.Conectar("A", "B");
            _grafo.Conectar("B", "C");
            _grafo.Conectar("C", "D");

            Assert.IsTrue(_grafo.MismaRed("A", "D"), "A y D estan unidos a traves de B y C");
            Assert.IsFalse(_grafo.MismaRed("A", "Z"));
        }

        [Test]
        public void Redes_AgrupaLosComponentesConexos()
        {
            _grafo.Conectar("A", "B");
            _grafo.Conectar("B", "C");
            _grafo.Conectar("X", "Y");

            List<HashSet<string>> redes = _grafo.Redes();

            Assert.AreEqual(2, redes.Count);
            Assert.IsTrue(redes.Exists(r => r.Count == 3 && r.Contains("A") && r.Contains("C")));
            Assert.IsTrue(redes.Exists(r => r.Count == 2 && r.Contains("X")));
        }

        // =================================================================================
        // Validación contra el cableado correcto
        // =================================================================================

        [Test]
        public void SinCablear_FaltaTodoYNoSobraNada()
        {
            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(0, informe.CablesPuestos);
            Assert.AreEqual(informe.CablesMinimos, informe.Faltantes.Count,
                "sin ningun cable, falta exactamente una union por cada cable minimo");
            Assert.AreEqual(0, informe.Sobrantes.Count);
        }

        [Test]
        public void CableadoCorrecto_NoDejaNiFaltantesNiSobrantes()
        {
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);

            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsTrue(informe.EsCorrecto, informe.ToString());
            Assert.AreEqual(informe.CablesMinimos, informe.CablesPuestos);
        }

        [Test]
        public void ElOrdenDeLosCablesDentroDeUnaRedNoImporta()
        {
            // La red de fase L2 es BRK-L2, S/L2, V1-B, V2-B, V3-B, V4-B.
            // Un alumno la cablea en cadena y otro en estrella: ambos son correctos.
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);
            var enCadena = VdfWiringSpec.Validar(_grafo);
            Assert.IsTrue(enCadena.EsCorrecto);

            var estrella = new VdfWiringGraph();
            var redes = VdfWiringSpec.Practica();
            for (int i = 0; i < redes.Count; i++)
            {
                // Todo colgando del primer borne en vez de en cadena.
                for (int j = 1; j < redes[i].Bornes.Length; j++)
                    estrella.Conectar(redes[i].Bornes[0], redes[i].Bornes[j]);
            }

            var informeEstrella = VdfWiringSpec.Validar(estrella);

            Assert.IsTrue(informeEstrella.EsCorrecto,
                "una topologia en estrella es electricamente equivalente:\n" + informeEstrella);
            Assert.AreEqual(enCadena.CablesPuestos, informeEstrella.CablesPuestos);
        }

        [Test]
        public void FaltaUnCable_SeReportaExactamenteEseYNadaMas()
        {
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);
            // Q0.6 -> S5 segun los esquemas del manual del modulo (seccion 3.4).
            _grafo.Desconectar("PLC-Q0.6", "S5");

            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(1, informe.Faltantes.Count, informe.ToString());
            Assert.IsTrue(informe.Faltantes[0].Contains("S5"));
            Assert.AreEqual(0, informe.Sobrantes.Count);
        }

        [Test]
        public void CableDeMas_EntreDosRedesSeReportaComoCortocircuito()
        {
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);
            _grafo.Conectar("VCC24", "GND24"); // +24 V contra 0 V

            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(0, informe.Faltantes.Count, "no falta nada, solo sobra");
            Assert.AreEqual(1, informe.Sobrantes.Count);
            Assert.IsTrue(informe.Sobrantes[0].Contains("CORTOCIRCUITO"), informe.Sobrantes[0]);
        }

        [Test]
        public void CableAUnBorneQueNoPintaNada_SeReportaComoSobrante()
        {
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);
            _grafo.Conectar("S1", "MP"); // MP no interviene en esta practica

            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(1, informe.Sobrantes.Count);
            Assert.IsTrue(informe.Sobrantes[0].Contains("MP"));
        }

        [Test]
        public void InvertirMarchaYMultivelocidad_SeDetecta()
        {
            // Error tipico: cruzar las salidas del PLC. El cableado correcto es
            // Q0.6 -> S5 y Q0.5 -> S6 (esquemas del manual, seccion 3.4); aqui se
            // intercambian para comprobar que la validacion lo ve.
            var redes = VdfWiringSpec.Practica();
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo, redes);
            _grafo.Desconectar("PLC-Q0.6", "S5");
            _grafo.Desconectar("PLC-Q0.5", "S6");
            _grafo.Conectar("PLC-Q0.6", "S6");
            _grafo.Conectar("PLC-Q0.5", "S5");

            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(2, informe.Faltantes.Count, informe.ToString());
            Assert.AreEqual(2, informe.Sobrantes.Count, informe.ToString());
        }

        [Test]
        public void ElInformeExplicaElProblemaEnCastellanoYNoSoloConIds()
        {
            var informe = VdfWiringSpec.Validar(_grafo);

            Assert.IsTrue(informe.Faltantes.Exists(f => f.Contains("Orden de marcha")),
                "los mensajes deben nombrar la red, no solo los bornes");
            Assert.IsTrue(informe.ToString().Contains("minimo necesario"));
        }

        // =================================================================================
        // Coherencia entre el cableado correcto y el layout
        // =================================================================================

        [Test]
        public void TodosLosBornesDelCableadoCorrectoExistenEnAlgunLayout()
        {
            var tablero = VdfPanelLayoutDefaults.Crear();
            var plc = VdfPanelLayoutDefaults.CrearPlc();

            // Los del motor no vienen de un layout: los crea el builder.
            var delMotor = new HashSet<string> { "MOT-U", "MOT-V", "MOT-W" };

            var redes = VdfWiringSpec.Practica();
            var faltan = new List<string>();

            for (int i = 0; i < redes.Count; i++)
            {
                foreach (string borne in redes[i].Bornes)
                {
                    bool existe = tablero.Buscar(borne) != null
                               || plc.Buscar(borne) != null
                               || delMotor.Contains(borne);
                    if (!existe) faltan.Add(borne + " (red: " + redes[i].Nombre + ")");
                }
            }

            UnityEngine.Object.DestroyImmediate(tablero);
            UnityEngine.Object.DestroyImmediate(plc);

            Assert.AreEqual(0, faltan.Count,
                "el cableado correcto referencia bornes que no estan en ningun layout:\n  "
                + string.Join("\n  ", faltan.ToArray()));
        }

        [Test]
        public void ElPlcTieneSusBornesYSonUnicos()
        {
            var plc = VdfPanelLayoutDefaults.CrearPlc();

            Assert.AreEqual(0, plc.Validar().Count, string.Join(" | ", plc.Validar().ToArray()));
            // Dos grupos de relé, como el 1214C: 1L con Q0.0..Q0.4 y 2L con Q0.5..Q0.7, mas
            // la fila de entradas 1M + I0.0..I0.4 para los pulsadores del tablero.
            Assert.AreEqual(16, plc.Bornes().Count,
                "1L + Q0.0..Q0.4, 2L + Q0.5..Q0.7, y 1M + I0.0..I0.4");
            Assert.IsNotNull(plc.Buscar("PLC-1L"));
            Assert.IsNotNull(plc.Buscar("PLC-2L"));
            Assert.IsNotNull(plc.Buscar("PLC-Q0.4"));
            Assert.IsNotNull(plc.Buscar("PLC-1M"));
            Assert.IsNotNull(plc.Buscar("PLC-I0.0"));
            Assert.IsNotNull(plc.Buscar("PLC-I0.4"));

            UnityEngine.Object.DestroyImmediate(plc);
        }

        [Test]
        public void CablesMinimos_SonLos19QueEsperaLaPractica()
        {
            Assert.AreEqual(19, VdfWiringSpec.CablesMinimos(VdfWiringSpec.Practica()));
        }
    }
}
