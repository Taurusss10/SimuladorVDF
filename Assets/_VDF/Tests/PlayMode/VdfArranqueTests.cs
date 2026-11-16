using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    public class VdfArranqueTests
    {
        private const string Escritorio = "VDF_Desktop";
        private const string Realidad = "VDF_VR";

        [UnityTest]
        public IEnumerator LaEscenaDeEscritorioArrancaSinExcepciones()
        {
            yield return CargarYAsentar(Escritorio);

            var sim = Object.FindAnyObjectByType<VdfSimulacion>();
            Assert.IsNotNull(sim, "no hay VdfSimulacion en la escena de escritorio");
        }

        [UnityTest]
        public IEnumerator LaEscenaDeVrArrancaSinExcepciones()
        {
            yield return CargarYAsentar(Realidad);

            var sim = Object.FindAnyObjectByType<VdfSimulacion>();
            Assert.IsNotNull(sim, "no hay VdfSimulacion en la escena de VR");
        }

        /// <summary>
        /// La secuencia de la práctica 4, de verdad y con el reloj corriendo: START habilita,
        /// SUBIR pide la velocidad 1 y el PLC la aplica tras su retardo.
        ///
        /// Lo que comprueba que EditMode no puede: que haya un Update vivo que descuente el
        /// retardo y que el variador acelere de verdad hacia la referencia.
        /// </summary>
        [UnityTest]
        public IEnumerator StartYSubirLlevanElMotorALaPrimeraVelocidad()
        {
            yield return CargarYAsentar(Escritorio);

            var sim = Object.FindAnyObjectByType<VdfSimulacion>();
            Assert.IsNotNull(sim, "no hay VdfSimulacion");
            sim.Despertar();

            // Se entra en la práctica de operación, como haría el alumno. La app abre en la
            // práctica 1, que es de observación y deja el módulo sin cablear y SIN TENSIÓN:
            // pulsar START ahí no puede mover el motor, y no debe.
            var runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();
            Assert.IsNotNull(runner, "no hay runner de prácticas");
            runner.Empezar(menu.Practicas[3]);
            yield return null;
            yield return null;

            // Se parte de un reposo forzado en vez de darlo por supuesto: la escena podria
            // venir con algo en marcha, y entonces el test fallaria por el motivo
            // equivocado. Lo que se mide aqui es la secuencia, no el estado inicial.
            sim.PulsarStop();
            yield return null;
            Assert.AreEqual(0, sim.Plc.NivelActivo, "tras STOP deberia quedar en reposo");

            sim.PulsarStart();
            Assert.IsTrue(sim.Plc.Habilitado, "START deberia habilitar el PLC");

            sim.PulsarSubir();

            // El retardo del enunciado es de 3 s; se espera algo mas por si el primer
            // fotograma de la escena tarda.
            float limite = PlcFanLogic.RetardoCambioNivelSegundos + 2f;
            float t = 0f;
            while (sim.Plc.NivelActivo < 1 && t < limite)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, sim.Plc.NivelActivo,
                "tras SUBIR y " + limite.ToString("0.0") + " s el PLC deberia estar en la "
                + "velocidad 1. Estado: " + sim.Resumen());

            Assert.IsTrue(sim.Plc.SalidaS1, "en marcha, la salida a S1 deberia estar activa");

            // Y el variador tiene que estar yendo a por esa referencia, no quieto.
            yield return new WaitForSeconds(1f);
            Assert.Greater(sim.Vfd.FrecuenciaSalidaHz, 0f,
                "el variador deberia estar acelerando. Estado: " + sim.Resumen());
        }

        /// <summary>
        /// STOP devuelve el sistema a reposo y deja la seleccion de velocidad a cero, que es
        /// lo que pide el enunciado: al volver a arrancar se empieza de nuevo.
        /// </summary>
        [UnityTest]
        public IEnumerator StopDevuelveATodoAReposo()
        {
            yield return CargarYAsentar(Escritorio);

            var sim = Object.FindAnyObjectByType<VdfSimulacion>();
            sim.Despertar();
            sim.PulsarStop();
            yield return null;
            sim.PulsarStart();
            sim.PulsarSubir();

            float t = 0f;
            while (sim.Plc.NivelActivo < 1 && t < PlcFanLogic.RetardoCambioNivelSegundos + 2f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            sim.PulsarStop();
            yield return null;

            Assert.IsFalse(sim.Plc.Habilitado, "tras STOP el PLC no deberia seguir habilitado");
            Assert.AreEqual(0, sim.Plc.NivelActivo, "tras STOP la velocidad deberia volver a 0");
            Assert.IsFalse(sim.Plc.SalidaS1, "tras STOP la orden de marcha deberia caer");
        }

        // =====================================================================================

        /// <summary>
        /// Carga la escena y le da unos fotogramas para que todo despierte. Sin la espera, la
        /// primera comprobacion cae antes de que corran los Start() de la escena.
        /// </summary>
        private static IEnumerator CargarYAsentar(string nombre)
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene(nombre, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);
        }
    }
}
