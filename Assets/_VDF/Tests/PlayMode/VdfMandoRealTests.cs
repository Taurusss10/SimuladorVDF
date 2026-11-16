using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    public class VdfMandoRealTests
    {
        private VdfSimulacion _sim;
        private VdfPracticaRunner _runner;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene("VDF_VR", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            VdfMandoSimulado.Preparar();
            yield return null;
            yield return null;
            VdfMandoSimulado.Conectar();
            yield return Frames(4);

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
        }

        [UnityTearDown]
        public IEnumerator Limpiar()
        {
            VdfMandoSimulado.Limpiar();
            yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Apunta, comprueba que el rayo lo tiene encima, aprieta y suelta.</summary>
        private static IEnumerator Pulsar(Vector3 objetivo, string nombreEsperado, bool lateral = false)
        {
            // Tiempos de persona, no de frames: un gatillo real se mantiene apretado una
            // décima de segundo o más, y así se ejercita igual que en el visor.
            VdfMandoSimulado.Apuntar(objetivo);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(nombreEsperado, VdfMandoSimulado.Apuntado(),
                "el rayo del mando no está sobre " + nombreEsperado);

            // Al FINAL del frame: así el evento se procesa al principio del siguiente, antes
            // de que XRI pregunte «¿se apretó en este frame?», como pasa en la Quest. Encolado
            // a mitad de frame, el Input System lo procesa en la actualización previa al
            // render (los mandos son dispositivos con seguimiento) y XRI nunca lo ve.
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.Apuntar(objetivo, gatillo: !lateral, lateral: lateral);
            yield return new WaitForSeconds(0.05f);
            UltimoApuntadoAlApretar = VdfMandoSimulado.Apuntado();
            UltimoEstado = VdfMandoSimulado.Estado();
            yield return new WaitForSeconds(0.25f);
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.Apuntar(objetivo);
            yield return new WaitForSeconds(0.2f);
        }

        private static string UltimoApuntadoAlApretar;
        private static string UltimoEstado;

        /// <summary>Para los mensajes de fallo: qué tenía el rayo encima al apretar.</summary>
        private static string Diagnostico()
        {
            return " [al apretar, el rayo estaba sobre: " + (UltimoApuntadoAlApretar ?? "nada")
                   + "; " + UltimoEstado + "]";
        }

        private static Vector3 Centro(string nombre)
        {
            var go = GameObject.Find(nombre);
            Assert.IsNotNull(go, "no existe " + nombre);
            var col = go.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : go.transform.position;
        }

        private static Vector3 Centro(Component c)
        {
            var col = c.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : c.transform.position;
        }

        private static void CerrarBienvenida()
        {
            var b = Object.FindAnyObjectByType<VdfPanelBienvenida>();
            if (b != null) b.Cerrar();
        }

        private VdfBotonPractica BotonDelMenu(int indice)
        {
            foreach (var b in Object.FindObjectsByType<VdfBotonPractica>())
                if (b.Menu != null && b.Indice == indice) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator ElGatillo_CierraLaBienvenida()
        {
            var bienvenida = Object.FindAnyObjectByType<VdfPanelBienvenida>();
            Assert.IsNotNull(bienvenida);
            var boton = bienvenida.transform.Find("Boton_98");

            yield return Pulsar(Centro(boton), "Boton_98");

            Assert.IsFalse(bienvenida.gameObject.activeSelf, "apuntar a EMPEZAR y apretar el gatillo no cerró la bienvenida" + Diagnostico());
        }

        [UnityTest]
        public IEnumerator ElLateral_EligeUnaPracticaEnElMenu()
        {
            CerrarBienvenida();
            var boton = BotonDelMenu(1);
            Assert.IsNotNull(boton);

            yield return Pulsar(Centro(boton), boton.name, lateral: true);

            Assert.AreEqual(2, _runner.Practica.Numero, "el gatillo lateral sobre la práctica 2 no la eligió" + Diagnostico());
        }

        [UnityTest]
        public IEnumerator ElGatillo_PulsaUnaTeclaDelVariador()
        {
            CerrarBienvenida();
            var vista = Object.FindAnyObjectByType<VfdKeypadView>();
            string antes = vista.Teclado.TextoPantalla;

            yield return Pulsar(Centro("Tecla_Arriba"), "Tecla_Arriba");

            Assert.AreNotEqual(antes, vista.Teclado.TextoPantalla, "el gatillo sobre ▲ no cambió la pantalla del V1000" + Diagnostico());
        }

        [UnityTest]
        public IEnumerator ElGatillo_PulsaStart()
        {
            CerrarBienvenida();
            _runner.Empezar(BotonDelMenu(3).Menu.Practicas[3]);
            yield return Frames(3);

            yield return Pulsar(Centro("Pulsador_START"), "Pulsador_START");

            Assert.IsTrue(_sim.Plc.Habilitado, "el gatillo sobre START no habilitó el PLC" + Diagnostico());

            // Y respondió con sonido: «no escucho nada, no sé si presioné».
            var fuente = GameObject.Find("VDF_SonidoPulsacion");
            Assert.IsNotNull(fuente, "pulsar START no generó el clic");
            var audio = fuente.GetComponent<AudioSource>();
            Assert.IsNotNull(audio);
            Assert.Greater(audio.volume, 0f, "el clic suena con volumen cero");
        }

        [UnityTest]
        public IEnumerator ElGatillo_SenalaUnBorne()
        {
            CerrarBienvenida();
            _runner.ConfirmarPasoManual();
            yield return Frames(2);
            Assert.AreEqual(1, _runner.PasoActual());

            VdfTerminal l1 = null;
            foreach (var t in Object.FindObjectsByType<VdfTerminal>()) if (t.Id == "BRK-L1") l1 = t;
            yield return Pulsar(Centro(l1), l1.name);

            Assert.AreEqual(2, _runner.PasoActual(), "señalar el breaker con el gatillo no cumplió el paso" + Diagnostico());
        }

        [UnityTest]
        public IEnumerator TocarConLaPunta_PulsaUnaTecla()
        {
            CerrarBienvenida();
            var vista = Object.FindAnyObjectByType<VfdKeypadView>();
            string antes = vista.Teclado.TextoPantalla;
            Vector3 tecla = Centro("Tecla_Arriba");

            VdfMandoSimulado.Tocar(tecla, 0.05f);
            yield return Frames(4);
            for (int i = 4; i >= 0; i--)
            {
                yield return new WaitForEndOfFrame();
                VdfMandoSimulado.Tocar(tecla, i * 0.01f - 0.004f);
                yield return new WaitForSeconds(0.03f);
            }
            VdfMandoSimulado.Tocar(tecla, 0.06f);
            yield return Frames(4);

            Assert.AreNotEqual(antes, vista.Teclado.TextoPantalla, "tocar ▲ con la punta del mando no hizo nada");
        }

        [UnityTest]
        public IEnumerator TocarConLaPunta_PulsaStart()
        {
            CerrarBienvenida();
            _runner.Empezar(BotonDelMenu(3).Menu.Practicas[3]);
            yield return Frames(3);
            Vector3 start = Centro("Pulsador_START");

            VdfMandoSimulado.Tocar(start, 0.05f);
            yield return new WaitForSeconds(0.1f);
            for (int i = 5; i >= -1; i--)
            {
                yield return new WaitForEndOfFrame();
                VdfMandoSimulado.Tocar(start, i * 0.01f);
                yield return new WaitForSeconds(0.03f);
            }
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.Tocar(start, 0.08f);
            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(_sim.Plc.Habilitado, "tocar START con la punta del mando no habilitó el PLC");
        }

        [UnityTest]
        public IEnumerator UnCable_SeEnchufaConElMando()
        {
            CerrarBienvenida();
            _runner.Empezar(BotonDelMenu(2).Menu.Practicas[2]);
            yield return new WaitForSeconds(0.3f);

            VdfXrPlug punta = null;
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                if (p.Conectado == null && (punta == null || p.transform.position.x < punta.transform.position.x)) punta = p;
            Assert.IsNotNull(punta, "no hay ninguna punta libre en la bandeja");

            VdfTerminal destino = null;
            foreach (var t in Object.FindObjectsByType<VdfTerminal>()) if (t.Id == "BRK-L1") destino = t;

            // Mano sobre la punta, y gatillo lateral.
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(punta.transform.position, false);
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(punta.transform.position, true);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(punta.name, VdfMandoSimulado.Sostenido(), "el gatillo lateral no cogió la punta");

            // Hasta el borne, en pasos, sin soltar.
            Vector3 desde = punta.transform.position, hasta = destino.transform.position;
            for (int i = 1; i <= 10; i++)
            {
                yield return new WaitForEndOfFrame();
                VdfMandoSimulado.LlevarMano(Vector3.Lerp(desde, hasta, i / 10f), true);
                yield return new WaitForSeconds(0.03f);
            }
            yield return new WaitForSeconds(0.2f);
            float distancia = Vector3.Distance(punta.transform.position, destino.transform.position);

            // Se suelta.
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(hasta, false);
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(destino, punta.Conectado,
                "soltar la punta sobre BRK-L1 no la enchufó (la punta quedó a "
                + (distancia * 1000f).ToString("F0") + " mm del borne; sostenida por: "
                + (VdfMandoSimulado.Sostenido() ?? "nada") + ")");
        }

        /// <summary>
        /// Soltar la punta CERCA del borne, a 2 cm, también la enchufa: el pulso de una mano
        /// en el visor no deja la punta en los 15 mm que captura el socket.
        /// </summary>
        [UnityTest]
        public IEnumerator UnCableSoltadoCerca_EncajaEnElBorneMasCercano()
        {
            CerrarBienvenida();
            _runner.Empezar(BotonDelMenu(2).Menu.Practicas[2]);
            yield return new WaitForSeconds(0.3f);

            VdfXrPlug punta = null;
            foreach (var p in Object.FindObjectsByType<VdfXrPlug>())
                if (p.Conectado == null && (punta == null || p.transform.position.x < punta.transform.position.x)) punta = p;
            VdfTerminal destino = null;
            foreach (var t in Object.FindObjectsByType<VdfTerminal>()) if (t.Id == "BRK-L1") destino = t;

            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(punta.transform.position, false);
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(punta.transform.position, true);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(punta.name, VdfMandoSimulado.Sostenido(), "el gatillo lateral no cogió la punta");

            // 2 cm delante del borne, hacia el alumno: fuera de lo que captura el socket.
            Vector3 desde = punta.transform.position, hasta = destino.transform.position + new Vector3(0f, 0f, -0.02f);
            for (int i = 1; i <= 10; i++)
            {
                yield return new WaitForEndOfFrame();
                VdfMandoSimulado.LlevarMano(Vector3.Lerp(desde, hasta, i / 10f), true);
                yield return new WaitForSeconds(0.03f);
            }
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();
            VdfMandoSimulado.LlevarMano(hasta, false);
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(destino, punta.Conectado, "soltada a 2 cm del borne, la punta no encajó");
        }

        /// <summary>Lo que hay que señalar brilla en amarillo desde que empieza el paso.</summary>
        [UnityTest]
        public IEnumerator LoQueHayQueSenalar_BrillaEnAmarillo()
        {
            CerrarBienvenida();
            _runner.ConfirmarPasoManual();

            VdfTerminal l1 = null;
            foreach (var t in Object.FindObjectsByType<VdfTerminal>()) if (t.Id == "BRK-L1") l1 = t;
            var r = l1.GetComponentInChildren<Renderer>();
            var bloque = new MaterialPropertyBlock();

            bool vistoAmarillo = false;
            float t0 = Time.time;
            while (Time.time - t0 < 2f && !vistoAmarillo)
            {
                r.GetPropertyBlock(bloque);
                Color c = bloque.GetColor("_BaseColor");
                if (c.r > 0.9f && c.g > 0.9f && c.b < 0.3f) vistoAmarillo = true;
                yield return null;
            }
            Assert.IsTrue(vistoAmarillo, "el borne que pide el paso no se puso amarillo en 2 s");
        }
    }
}
