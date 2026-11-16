using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 3: la pantalla de siete segmentos.
    ///
    /// El test que de verdad importa aquí es
    /// <see cref="TodoLoQuePuedeMostrarElTeclado_SePuedeDibujarEnSieteSegmentos"/>: recorre los
    /// mensajes que la lógica es capaz de producir y comprueba que la pantalla puede
    /// dibujarlos. Un display real no puede mostrar cualquier texto, y si alguien añade un
    /// mensaje con una letra imposible, el alumno vería un hueco en blanco sin que nada avise.
    /// </summary>
    public class VfdSieteSegmentosTests
    {
        // =====================================================================================
        // Tipografía
        // =====================================================================================

        [Test]
        public void LosDiezDigitos_TienenPatronesDistintos()
        {
            var vistos = new System.Collections.Generic.HashSet<int>();
            for (char c = '0'; c <= '9'; c++)
            {
                int p = VfdSieteSegmentos.Patron(c);
                Assert.AreNotEqual(0, p, "El dígito " + c + " no tiene patrón.");
                Assert.IsTrue(vistos.Add(p), "Dos dígitos comparten patrón: " + c);
            }
        }

        [Test]
        public void ElOcho_EnciendeLosSieteSegmentos()
        {
            Assert.AreEqual(0x7F, VfdSieteSegmentos.Patron('8'));
        }

        [Test]
        public void ElUno_EnciendeSoloLasDosVerticalesDerechas()
        {
            // bits 1 y 2 = segmentos b y c
            Assert.AreEqual(0x06, VfdSieteSegmentos.Patron('1'));
        }

        [Test]
        public void ElGuion_EnciendeSoloElSegmentoCentral()
        {
            Assert.AreEqual(0x40, VfdSieteSegmentos.Patron('-'));
        }

        [Test]
        public void ElEspacioYLoDesconocido_DejanElDigitoApagado()
        {
            Assert.AreEqual(0, VfdSieteSegmentos.Patron(' '));
            Assert.AreEqual(0, VfdSieteSegmentos.Patron('#'));
            Assert.AreEqual(0, VfdSieteSegmentos.Patron('@'));
        }

        // =====================================================================================
        // Reparto en celdas
        // =====================================================================================

        private static int Repartir(string texto, out int[] patrones, out bool[] puntos)
        {
            patrones = new int[VfdKeypad.Digitos];
            puntos = new bool[VfdKeypad.Digitos];
            return VfdSieteSegmentos.Repartir(texto, patrones, puntos);
        }

        [Test]
        public void ElPuntoDecimal_NoConsumeUnDigito()
        {
            int[] patrones; bool[] puntos;
            int celdas = Repartir("F00.00", out patrones, out puntos);

            // "F00.00" son seis caracteres pero cinco celdas: el punto se engancha al 0
            // anterior, igual que en el equipo. Si consumiera celda, no cabría en el V1000.
            Assert.AreEqual(5, celdas);
            Assert.IsTrue(puntos[2], "El punto debería ir en la tercera celda.");
            Assert.AreEqual(VfdSieteSegmentos.Patron('F'), patrones[0]);
            Assert.AreEqual(VfdSieteSegmentos.Patron('0'), patrones[4]);
        }

        [Test]
        public void UnPuntoSinCaracterDelante_OcupaSuPropiaCelda()
        {
            int[] patrones; bool[] puntos;
            int celdas = Repartir(".5", out patrones, out puntos);

            Assert.AreEqual(2, celdas);
            Assert.IsTrue(puntos[0]);
            Assert.AreEqual(0, patrones[0], "La celda del punto suelto no lleva dígito.");
            Assert.AreEqual(VfdSieteSegmentos.Patron('5'), patrones[1]);
        }

        [Test]
        public void UnTextoMasLargoQueLaPantalla_SeRecortaSinDesbordar()
        {
            int[] patrones; bool[] puntos;
            int celdas = Repartir("1234567890", out patrones, out puntos);

            Assert.AreEqual(VfdKeypad.Digitos, celdas,
                "No debe escribir más celdas que dígitos tiene la pantalla.");
        }

        [Test]
        public void UnTextoVacio_DejaLaPantallaApagada()
        {
            int[] patrones; bool[] puntos;
            Assert.AreEqual(0, Repartir("", out patrones, out puntos));
            Assert.AreEqual(0, Repartir(null, out patrones, out puntos));

            for (int i = 0; i < patrones.Length; i++)
                Assert.AreEqual(0, patrones[i]);
        }

        // =====================================================================================
        // El test que importa
        // =====================================================================================

        [Test]
        public void TodoLoQuePuedeMostrarElTeclado_SePuedeDibujarEnSieteSegmentos()
        {
            // Mensajes y códigos que la lógica puede producir, más los identificadores de
            // todos los parámetros de la tabla, que es lo que se ve al recorrer -PAr-.
            var textos = new System.Collections.Generic.List<string>
            {
                "F00.00", "-----", "End", "oPE", "rE", "n.SIM",
                "Uv1", "oL1", "EF0"
            };
            textos.AddRange(VfdKeypad.Modos);

            foreach (var def in VfdParameterTable.Todos)
                textos.Add(def.Id);

            foreach (var texto in textos)
            {
                foreach (char c in texto)
                {
                    if (c == '.' || c == ',' || c == ' ') continue;

                    Assert.AreNotEqual(0, VfdSieteSegmentos.Patron(c),
                        "El carácter '" + c + "' de \"" + texto + "\" no se puede dibujar en " +
                        "siete segmentos, así que saldría un hueco en blanco en la pantalla.");
                }
            }
        }

        [Test]
        public void CadaIdentificadorDeParametro_CabeEnLaPantalla()
        {
            foreach (var def in VfdParameterTable.Todos)
            {
                int[] patrones; bool[] puntos;
                int celdas = Repartir(def.Id, out patrones, out puntos);

                Assert.LessOrEqual(celdas, VfdKeypad.Digitos,
                    "El identificador " + def.Id + " no cabe en los " + VfdKeypad.Digitos +
                    " dígitos del operador.");
            }
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        [Test]
        public void Construir_CreaSieteSegmentosYUnPuntoPorDigito()
        {
            var padre = new GameObject("PadrePrueba");
            try
            {
                var panel = VfdSieteSegmentos.Construir(padre.transform, VfdKeypad.Digitos,
                                                        0.074f, 0.0155f, 0f);

                Assert.IsNotNull(panel);
                Assert.IsTrue(panel.Construida);
                Assert.AreEqual(VfdKeypad.Digitos, panel.NumeroDigitos);

                var renderers = panel.GetComponentsInChildren<Renderer>();
                Assert.AreEqual(VfdKeypad.Digitos * VfdSieteSegmentos.PartesPorDigito,
                                renderers.Length,
                                "Deben ser siete segmentos más el punto por cada dígito.");
            }
            finally
            {
                Object.DestroyImmediate(padre);
            }
        }

        [Test]
        public void LosSegmentos_NoTienenCollider()
        {
            var padre = new GameObject("PadrePrueba");
            try
            {
                var panel = VfdSieteSegmentos.Construir(padre.transform, VfdKeypad.Digitos,
                                                        0.074f, 0.0155f, 0f);

                // Si los segmentos tuvieran collider interceptarían el rayo del ratón y el
                // del mando, y pulsar una tecla a través de la pantalla dejaría de funcionar.
                Assert.AreEqual(0, panel.GetComponentsInChildren<Collider>().Length);
            }
            finally
            {
                Object.DestroyImmediate(padre);
            }
        }

        [Test]
        public void Mostrar_NoRevientaSiLaPantallaNoEstaConstruida()
        {
            var go = new GameObject("SinConstruir");
            try
            {
                var panel = go.AddComponent<VfdSieteSegmentos>();
                Assert.IsFalse(panel.Construida);
                Assert.DoesNotThrow(() => panel.Mostrar("F00.00", -1, true, false));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
