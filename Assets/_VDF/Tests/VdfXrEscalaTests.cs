using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Que las ayudas de VR sigan cabiendo en el tablero.
    ///
    /// Por qué existe este test: el tablero se construyó al principio con piezas infladas (el
    /// V1000 medía 190 x 300 mm cuando el real mide 68 x 128) y los sockets de VR se dimensionaron
    /// para ese tablero inflado, con 30 mm de radio. Al pasar a las medidas de catálogo los bornes
    /// quedaron a 18 mm de paso, así que cada socket se tragaba también a sus dos vecinos: en el
    /// visor era imposible cablear el borne que se quería y el fallo no lo veía nadie, porque el
    /// desarrollador no tiene visor.
    ///
    /// La regla es geométrica, no estética: si el radio llega al borne de al lado, sobra ayuda.
    /// </summary>
    public class VdfXrEscalaTests
    {
        /// <summary>Distancia, en el plano del tablero, entre los dos bornes más juntos.</summary>
        private static float PasoMinimoEntreBornes(List<VdfPanelElement> elementos)
        {
            var bornes = new List<VdfPanelElement>();
            for (int i = 0; i < elementos.Count; i++)
                if (elementos[i].EsConectable) bornes.Add(elementos[i]);

            Assert.Greater(bornes.Count, 1, "el tablero tiene que traer bornes");

            float minimo = float.MaxValue;
            for (int i = 0; i < bornes.Count; i++)
                for (int j = i + 1; j < bornes.Count; j++)
                {
                    float d = Vector2.Distance(bornes[i].Posicion, bornes[j].Posicion);
                    if (d < minimo) minimo = d;
                }

            return minimo;
        }

        [Test]
        public void ElSocketNoAlcanzaAlBorneDeAlLado()
        {
            float paso = PasoMinimoEntreBornes(VdfPanelLayoutDefaults.CrearElementos());

            Assert.Less(VdfXrSetup.RadioSocket, paso * 0.5f,
                "con el radio por encima de la mitad del paso (" + (paso * 500f).ToString("0.0") +
                " mm) un socket agarra también al borne vecino");
        }

        [Test]
        public void EnElPlcTampocoAlcanzaAlDeAlLado()
        {
            // El PLC tiene sus bornes aún más juntos que el tablero, así que es el caso límite.
            float paso = PasoMinimoEntreBornes(VdfPanelLayoutDefaults.CrearElementosPlc());

            Assert.Less(VdfXrSetup.RadioSocket, paso * 0.5f,
                "los bornes del PLC están a " + (paso * 1000f).ToString("0.0") + " mm");
        }

        [Test]
        public void ElConectorSeVeSinTaparAlVecino()
        {
            // Tiene que ser mayor que el borne, para distinguirlo al agarrarlo, y menor que el
            // paso, para no esconder el de al lado mientras se cablea.
            float borne = VdfPanelElement.TamanoPorDefecto(VdfElementType.Borne).x;
            float paso = PasoMinimoEntreBornes(VdfPanelLayoutDefaults.CrearElementos());

            Assert.Greater(VdfXrSetup.TamanoConector, borne * 0.5f, "se tiene que ver");
            Assert.Less(VdfXrSetup.TamanoConector, paso, "y no puede tapar al vecino");
        }

        [Test]
        public void ElTableroMantieneLasMedidasDeCatalogo()
        {
            // Las medidas vienen de los manuales, no de lo que quedaba bonito: V1000 200 V de
            // bastidor pequeño 68 x 128 mm (SIEP C710606 18F, tabla 2.3) y magnetotérmico
            // tripolar DIN 52,5 x 85 mm. Si alguien las vuelve a inflar, salta aquí.
            Vector2 variador = VdfPanelElement.TamanoPorDefecto(VdfElementType.Variador);
            Assert.AreEqual(0.068f, variador.x, 0.001f, "ancho del V1000");
            Assert.AreEqual(0.128f, variador.y, 0.001f, "alto del V1000");

            var layout = VdfPanelLayoutDefaults.Crear();
            Assert.Less(layout.Ancho, 0.60f, "el tablero real no llega a 60 cm de ancho");
            Assert.Less(layout.Alto, 0.45f, "ni a 45 cm de alto");
        }
    }
}
