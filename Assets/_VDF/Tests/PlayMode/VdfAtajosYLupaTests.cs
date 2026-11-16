using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    /// <summary>
    /// Los botones libres del mando: A, B y el joystick derecho programan el V1000 mientras el
    /// rayo lo apunta, y X e Y manejan la lupa de la mano izquierda. Entran por el Input System
    /// con mandos simulados, el mismo camino que en la Quest.
    /// </summary>
    public class VdfAtajosYLupaTests
    {
        private VdfSimulacion _sim;
        private VdfXrAtajosV1000 _atajos;
        private VdfXrLupa _lupa;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Interruptores;
            SceneManager.LoadScene("VDF_VR", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            VdfMandoSimulado.Preparar();
            yield return null;
            yield return null;
            VdfMandoSimulado.Conectar();
            yield return Frames(4);

            var bienvenida = Object.FindAnyObjectByType<VdfPanelBienvenida>();
            if (bienvenida != null) bienvenida.Cerrar();

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _atajos = Object.FindAnyObjectByType<VdfXrAtajosV1000>();
            _lupa = Object.FindAnyObjectByType<VdfXrLupa>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Limpiar()
        {
            VdfMandoSimulado.Limpiar();
            VdfSimulacion.ModoMandoForzado = null;
            yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Centro de una tecla del V1000, para apuntarle con el rayo.</summary>
        private Vector3 TeclaDelVariador()
        {
            Transform variador = _sim.Operador.transform.parent != null ? _sim.Operador.transform.parent : _sim.Operador.transform;
            foreach (var poke in variador.GetComponentsInChildren<VdfXrKeyPoke>())
            {
                var col = poke.GetComponent<Collider>();
                return col != null ? col.bounds.center : poke.transform.position;
            }
            Assert.Fail("el V1000 no tiene teclas accionables con el mando");
            return Vector3.zero;
        }

        [Test]
        public void LaEscenaDeVrMontaLosAtajosYLaLupa()
        {
            Assert.IsNotNull(_atajos, "la escena de VR debería crear los atajos del V1000 al arrancar");
            Assert.IsNotNull(_lupa, "la escena de VR debería crear la lupa al arrancar");
            Assert.IsFalse(_lupa.Visible, "la lupa empieza oculta");
        }

        [UnityTest]
        public IEnumerator ApuntandoAlVariadorElJoystickYAManejanElTeclado()
        {
            Vector3 tecla = TeclaDelVariador();
            VdfMandoSimulado.Apuntar(tecla);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(_atajos.Activo, "con el rayo sobre el V1000 los atajos deberían estar activos. Apuntado: "
                                          + VdfMandoSimulado.Apuntado());

            var teclado = _sim.Operador.Teclado;
            string antes = teclado.TextoPantalla;
            VdfMandoSimulado.Apuntar(tecla, false, false, false, false, new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.15f);
            VdfMandoSimulado.Apuntar(tecla);
            yield return new WaitForSeconds(0.15f);
            Assert.AreNotEqual(antes, teclado.TextoPantalla, "el joystick arriba debería hacer de ▲");

            var estadoAntes = teclado.Estado;
            VdfMandoSimulado.Apuntar(tecla, false, false, true, false, Vector2.zero);
            yield return new WaitForSeconds(0.15f);
            VdfMandoSimulado.Apuntar(tecla);
            yield return new WaitForSeconds(0.15f);
            Assert.AreNotEqual(estadoAntes, teclado.Estado, "A debería hacer de ENTER y entrar en el menú elegido");
        }

        [UnityTest]
        public IEnumerator MientrasSeApuntaAlVariadorElJoystickNoTeletransporta()
        {
            Assert.IsNotNull(_atajos.LocomocionDerechaHabilitada(), "el rig no tiene acciones de locomoción derecha");
            Assert.IsTrue(_atajos.LocomocionDerechaHabilitada().Value, "fuera del variador el teletransporte está disponible");

            VdfMandoSimulado.Apuntar(TeclaDelVariador());
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(_atajos.LocomocionDerechaHabilitada().Value, "apuntando al V1000 el joystick no debe teletransportar");

            // Se aparta el rayo del variador: tras el margen de gracia todo vuelve a ser como antes.
            VdfMandoSimulado.Apuntar(TeclaDelVariador() + new Vector3(0f, 1.5f, 0f));
            yield return new WaitForSeconds(VdfXrAtajosV1000.Gracia + 0.3f);
            Assert.IsFalse(_atajos.Activo, "sin apuntar al variador los atajos se desactivan");
            Assert.IsTrue(_atajos.LocomocionDerechaHabilitada().Value, "y el joystick vuelve a teletransportar");
        }

        [UnityTest]
        public IEnumerator SinApuntarAlVariadorElBotonANoTocaElTeclado()
        {
            VdfMandoSimulado.Apuntar(TeclaDelVariador() + new Vector3(0f, 1.5f, 0f));
            yield return new WaitForSeconds(0.2f);
            var teclado = _sim.Operador.Teclado;
            string antes = teclado.TextoPantalla;
            var estadoAntes = teclado.Estado;

            VdfMandoSimulado.Apuntar(TeclaDelVariador() + new Vector3(0f, 1.5f, 0f), false, false, true, true, new Vector2(0f, 1f));
            yield return Frames(4);

            Assert.IsFalse(_atajos.Activo);
            Assert.AreEqual(antes, teclado.TextoPantalla, "sin apuntar al V1000, A, B y el joystick no deben tocarlo");
            Assert.AreEqual(estadoAntes, teclado.Estado);
        }

        [UnityTest]
        public IEnumerator XMuestraLaLupaEYCambiaElAumento()
        {
            Vector3 mano = Camera.main != null ? Camera.main.transform.position + new Vector3(-0.15f, -0.25f, 0.30f)
                                               : new Vector3(-0.15f, 1.1f, -0.6f);

            VdfMandoSimulado.ManoIzquierda(mano, true, false);
            yield return new WaitForSeconds(0.15f);
            VdfMandoSimulado.ManoIzquierda(mano, false, false);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_lupa.Visible, "X debería mostrar la lupa");
            Assert.AreEqual(2f, _lupa.Aumento, "la lupa empieza en 2×");

            foreach (float esperado in new[] { 3f, 4f, 2f })
            {
                VdfMandoSimulado.ManoIzquierda(mano, false, true);
                yield return new WaitForSeconds(0.15f);
                VdfMandoSimulado.ManoIzquierda(mano, false, false);
                yield return new WaitForSeconds(0.15f);
                Assert.AreEqual(esperado, _lupa.Aumento, "Y debería pasar al aumento siguiente");
            }

            VdfMandoSimulado.ManoIzquierda(mano, true, false);
            yield return new WaitForSeconds(0.15f);
            VdfMandoSimulado.ManoIzquierda(mano, false, false);
            yield return new WaitForSeconds(0.15f);
            Assert.IsFalse(_lupa.Visible, "X otra vez debería ocultarla");
        }

        [UnityTest]
        public IEnumerator LaLupaDibujaLoQueTieneDelante()
        {
            _lupa.Alternar();
            yield return new WaitForSeconds(0.15f);
            var camara = _lupa.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camara, "la lupa necesita su cámara");
            Assert.IsTrue(camara.enabled, "con la lupa visible la cámara dibuja");
            Assert.IsNotNull(camara.targetTexture, "y dibuja sobre una textura, no sobre la pantalla");
            // Como una lupa de verdad: la cámara mira desde los ojos a través de la lente, con el
            // tamaño aparente de la lente dividido por el aumento.
            var ojos = Camera.main;
            Assert.IsNotNull(ojos, "la escena de VR necesita su cámara principal");
            Assert.Less(Vector3.Distance(ojos.transform.position, camara.transform.position), 0.001f,
                        "la cámara de la lupa debe estar en los ojos del estudiante");
            var lente = _lupa.transform.Find("Lente");
            float distancia = Vector3.Distance(ojos.transform.position, lente.position);
            Assert.AreEqual(VdfXrLupa.CampoDeVision(distancia, 2f), camara.fieldOfView, 0.05f,
                            "a 2× el campo es la mitad del tamaño aparente de la lente");
            Assert.Greater(camara.nearClipPlane, distancia, "la cámara no debe verse la mano ni la lupa");

            float campo2 = camara.fieldOfView;
            _lupa.SiguienteAumento();
            yield return null;
            Assert.AreEqual(campo2 * 2f / 3f, camara.fieldOfView, 0.05f, "a 3× el campo se reduce en proporción");

            _lupa.Alternar();
            yield return null;
            Assert.IsFalse(camara.enabled, "oculta, la cámara no gasta nada");
        }
    }
}
