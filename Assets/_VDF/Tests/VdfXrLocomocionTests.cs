using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 8: las estaciones de teletransporte.
    ///
    /// Lo que se puede equivocar aquí no es la lógica, son las distancias, y eso se nota
    /// solo con el visor puesto. Como no hay visor, estos tests fijan las reglas que se
    /// comprobaron con capturas desde el ojo del alumno: delante del banco, dentro de la
    /// sala, y a la distancia que pide cada tarea.
    /// </summary>
    public class VdfXrLocomocionTests
    {
        /// <summary>Cara delantera del banco, medida en la escena.</summary>
        private const float FrenteDelBanco = -0.60f;

        [Test]
        public void HayUnaEstacionPorTarea()
        {
            var e = VdfXrLocomocion.Estaciones();

            Assert.AreEqual(4, e.Count);
            Assert.IsTrue(e.Exists(x => x.Nombre.Contains("tablero")), "vista general");
            Assert.IsTrue(e.Exists(x => x.Nombre.Contains("V1000")), "teclado");
            Assert.IsTrue(e.Exists(x => x.Nombre.Contains("motor")), "salida a motor");
            Assert.IsTrue(e.Exists(x => x.Nombre.Contains("Ladder")), "diagrama");
        }

        [Test]
        public void NingunaEstacionCaeDentroDelBanco()
        {
            // Teletransportarse dentro del mueble deja al alumno con la cabeza en la madera.
            foreach (var e in VdfXrLocomocion.Estaciones())
                Assert.Less(e.Posicion.z, FrenteDelBanco,
                    "La estación '" + e.Nombre + "' está sobre el banco o detrás de su cara.");
        }

        [Test]
        public void TodasLasEstacionesEstanEnElSuelo()
        {
            foreach (var e in VdfXrLocomocion.Estaciones())
                Assert.AreEqual(0f, e.Posicion.y, 0.001f,
                    "El ancla se levanta 5 mm al montarla; el dato tiene que estar a ras de suelo.");
        }

        [Test]
        public void LasEstacionesDeTrabajoQuedanAlAlcanceDelBrazo()
        {
            // 0,85 m es lo que alcanza un brazo inclinándose un poco sobre el banco. La
            // primera versión las puso a 1 m "por seguridad" y desde ahí no se llegaba a
            // tocar ni una tecla.
            foreach (var e in VdfXrLocomocion.Estaciones())
            {
                if (!e.Nombre.Contains("V1000") && !e.Nombre.Contains("motor")) continue;

                var ojos = e.Posicion + new Vector3(0f, 1.36f, 0f);
                float d = Vector3.Distance(ojos, e.Mirando);
                Assert.Less(d, 0.85f, "Desde '" + e.Nombre + "' no se llega: " + d.ToString("F2") + " m");
            }
        }

        [Test]
        public void LaEstacionDelLadderSeSeparaLoSuficienteParaVerElDiagramaEntero()
        {
            // El panel mide 1,74 m de alto. A menos de metro y medio no cabe en el campo de
            // visión, que fue lo que pasó con la primera colocación.
            foreach (var e in VdfXrLocomocion.Estaciones())
            {
                if (!e.Nombre.Contains("Ladder")) continue;

                var ojos = e.Posicion + new Vector3(0f, 1.36f, 0f);
                Assert.Greater(Vector3.Distance(ojos, e.Mirando), 1.5f);
            }
        }

        [Test]
        public void LasEstacionesNoSePisanEntreSi()
        {
            // Dos anclas solapadas se roban el rayo entre ellas, igual que les pasaba a los
            // bornes del motor.
            var e = VdfXrLocomocion.Estaciones();
            float minimo = VdfXrLocomocion.RadioAncla * 2f;

            for (int i = 0; i < e.Count; i++)
                for (int j = i + 1; j < e.Count; j++)
                {
                    float d = Vector3.Distance(e[i].Posicion, e[j].Posicion);
                    Assert.Greater(d, minimo,
                        "'" + e[i].Nombre + "' y '" + e[j].Nombre + "' se solapan (" + d.ToString("F2") + " m).");
                }
        }

        [Test]
        public void CadaEstacionMiraASuObjetivo()
        {
            foreach (var e in VdfXrLocomocion.Estaciones())
            {
                Vector3 hacia = e.Mirando - e.Posicion;
                hacia.y = 0f;
                Assert.Greater(hacia.sqrMagnitude, 0.0001f,
                    "'" + e.Nombre + "' no tiene a dónde mirar: el ancla se quedaría con la " +
                    "rotación por defecto.");
            }
        }
    }
}
