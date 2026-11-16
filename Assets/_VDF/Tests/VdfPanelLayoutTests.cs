using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// E3: el layout es dato, así que se puede validar sin construir nada en la escena.
    /// Estos tests son la red de seguridad para cuando se ajusten las coordenadas con
    /// las fotos reales: si alguien mueve un borne encima de otro, salta aquí.
    /// </summary>
    public class VdfPanelLayoutTests
    {
        private VdfPanelLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = VdfPanelLayoutDefaults.Crear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_layout != null) Object.DestroyImmediate(_layout);
        }

        [Test]
        public void LayoutPorDefecto_NoTieneProblemasEstructurales()
        {
            List<string> problemas = _layout.Validar();

            Assert.AreEqual(0, problemas.Count,
                "El layout tiene problemas:\n  " + string.Join("\n  ", problemas.ToArray()));
        }

        [Test]
        public void TodosLosElementosTienenIdUnico()
        {
            var vistos = new HashSet<string>();
            foreach (var e in _layout.Elementos)
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.Id), "Hay un elemento sin Id");
                Assert.IsTrue(vistos.Add(e.Id), "Id duplicado: " + e.Id);
            }
        }

        [Test]
        public void RegletaFila1_TieneLos15BornesDelManual()
        {
            Assert.AreEqual(15, VdfPanelLayoutDefaults.RegletaFila1.Length);

            int enLayout = _layout.Elementos.FindAll(e => e.Grupo == "Regleta fila 1").Count;
            Assert.AreEqual(15, enLayout, "deberian ser 15 bornes en la fila 1");
        }

        [Test]
        public void RegletaFila2_TieneLos15BornesDeLaFoto()
        {
            // El manual del modulo lista 17 porque mete U, V y W en esta fila. En el modulo
            // real la salida a motor esta aparte, bajo el variador, y la fila tiene 15.
            Assert.AreEqual(15, VdfPanelLayoutDefaults.RegletaFila2.Length);

            int enLayout = _layout.Elementos.FindAll(e => e.Grupo == "Regleta fila 2").Count;
            Assert.AreEqual(15, enLayout, "deberian ser 15 bornes en la fila 2");

            foreach (string uvw in new[] { "U", "V", "W" })
                Assert.IsFalse(System.Array.Exists(VdfPanelLayoutDefaults.RegletaFila2,
                    x => x == uvw), uvw + " no va en la regleta, va bajo el variador");
        }

        [Test]
        public void ElPlcTieneLasEntradasDeLosPulsadores()
        {
            var plc = VdfPanelLayoutDefaults.CrearPlc();

            Assert.AreEqual(VdfPanelLayoutDefaults.PlcEntradas.Length - 1,
                VdfPanelLayoutDefaults.PlcEntradaDe.Length,
                "cada entrada menos el comun tiene que decir a que pulsador va");

            foreach (string borne in VdfPanelLayoutDefaults.PlcEntradas)
                Assert.IsNotNull(plc.Buscar("PLC-" + borne),
                    "Falta la entrada " + borne + " en el PLC");
        }

        [Test]
        public void RegletaFila1_RepiteLaEtiquetaACPeroConIdsDistintos()
        {
            var ac = _layout.Elementos.FindAll(e => e.Etiqueta == "AC" && e.Grupo == "Regleta fila 1");

            Assert.AreEqual(2, ac.Count, "el manual lista AC dos veces en la fila 1");
            Assert.AreNotEqual(ac[0].Id, ac[1].Id, "las dos AC deben tener Id distinto");
        }

        [Test]
        public void ExistenLosBornesQueUsaElCableadoDeLaPractica()
        {
            // Los que menciona la seccion "Cableado correcto" del enunciado.
            string[] imprescindibles =
            {
                "R/L1", "S/L2",               // entrada del variador: dos bornes, no tres
                "U_MOT", "V_MOT", "W_MOT",    // salida a motor
                "VCC24", "GND24", "TIERRA",   // alimentacion de control
                "S1", "S5", "S6", "SC",       // entradas digitales del variador
                "BRK-L1", "BRK-L2", "BRK-L3"  // salida del breaker
            };

            foreach (string id in imprescindibles)
                Assert.IsNotNull(_layout.Buscar(id), "Falta el borne " + id + " en el layout");
        }

        [Test]
        public void TodosLosPilotosEIndicadoresEstanPresentes()
        {
            string[] pilotos = { "FUNC", "FALLA", "E", "V1", "V2", "V3", "V4" };

            foreach (string id in pilotos)
            {
                var e = _layout.Buscar(id);
                Assert.IsNotNull(e, "Falta el piloto " + id);
                Assert.AreEqual(VdfElementType.PilotoLed, e.Tipo, id + " deberia ser un piloto");
            }
        }

        [Test]
        public void LosPulsadoresDeMandoEstanPresentesYPeEsHongo()
        {
            Assert.AreEqual(VdfElementType.Pulsador, _layout.Buscar("START").Tipo);
            Assert.AreEqual(VdfElementType.Pulsador, _layout.Buscar("STOP").Tipo);
            Assert.AreEqual(VdfElementType.Pulsador, _layout.Buscar("SUBIR").Tipo);
            Assert.AreEqual(VdfElementType.Pulsador, _layout.Buscar("BAJAR").Tipo);
            Assert.AreEqual(VdfElementType.PulsadorHongo, _layout.Buscar("PE").Tipo,
                "la parada de emergencia debe ser de tipo hongo");
        }

        [Test]
        public void CadaPulsadorYPilotoTieneSuParDeBorneras()
        {
            string[] conBornes = { "FUNC", "FALLA", "E", "SUBIR", "BAJAR" };

            foreach (string id in conBornes)
            {
                Assert.IsNotNull(_layout.Buscar(id + "-A"), id + " no tiene bornera A");
                Assert.IsNotNull(_layout.Buscar(id + "-B"), id + " no tiene bornera B");
            }
        }

        [Test]
        public void SoloLosBornesSonConectables()
        {
            foreach (var e in _layout.Elementos)
            {
                bool esperado = e.Tipo == VdfElementType.Borne;
                Assert.AreEqual(esperado, e.EsConectable,
                    e.Id + " (" + e.Tipo + ") no deberia ser conectable");
            }
        }

        [Test]
        public void HayUnNumeroRazonableDeBornes()
        {
            int bornes = _layout.Bornes().Count;

            // 15 + 15 de regleta, 6 de los pilotos de estado, 4 de subir/bajar,
            // 8 de V1..V4, 2 de entrada, 4 de salida a motor, 3 del magnetotermico,
            // 2 de 24 V y 1 de tierra.
            Assert.AreEqual(60, bornes, "numero de bornes conectables del tablero");
        }

        [Test]
        public void MoverUnBorneEncimaDeOtro_LoDetectaLaValidacion()
        {
            // Comprueba que la red de seguridad realmente salta.
            var a = _layout.Buscar("S1");
            var b = _layout.Buscar("S2");
            a.Posicion = b.Posicion;

            var problemas = _layout.Validar();

            Assert.IsTrue(problemas.Exists(p => p.Contains("Solapan")),
                "deberia detectarse el solapamiento");
        }

        [Test]
        public void SacarUnElementoDeLaPlaca_LoDetectaLaValidacion()
        {
            _layout.Buscar("S1").Posicion = new Vector2(5f, 5f);

            var problemas = _layout.Validar();

            Assert.IsTrue(problemas.Exists(p => p.Contains("se sale de la placa")));
        }
    }
}
