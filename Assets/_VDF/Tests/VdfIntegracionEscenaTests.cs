using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    public class VdfIntegracionEscenaTests
    {
        private GameObject _raiz;

        [SetUp]
        public void SetUp()
        {
            _raiz = new GameObject("RaizIntegracion");
        }

        [TearDown]
        public void TearDown()
        {
            if (_raiz != null) Object.DestroyImmediate(_raiz);
        }

        private T Crear<T>(string nombre) where T : Component
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(_raiz.transform, false);
            return go.AddComponent<T>();
        }

        private VdfTerminal Borne(string id)
        {
            var t = Crear<VdfTerminal>("Borne_" + id);
            t.Id = id;
            return t;
        }

        // =====================================================================================
        // 1. Los botones del menú tienen que poder pulsarse por código
        // =====================================================================================

        [Test]
        public void ElBotonDelMenu_ArrancaLaPracticaAlPulsarlo()
        {
            var runner = Crear<VdfPracticaRunner>("Runner");
            var menu = Crear<VdfMenuPracticas>("Menu");
            menu.Runner = runner;

            var p1 = ScriptableObject.CreateInstance<VdfPractica>();
            var p2 = ScriptableObject.CreateInstance<VdfPractica>();
            try
            {
                p1.Numero = 1; p1.Titulo = "Uno";
                p2.Numero = 2; p2.Titulo = "Dos";
                menu.Practicas.Add(p1);
                menu.Practicas.Add(p2);

                menu.Seleccionar(1);

                Assert.AreSame(p2, runner.Practica,
                    "Pulsar un botón tiene que arrancar su práctica.");
            }
            finally
            {
                Object.DestroyImmediate(p1);
                Object.DestroyImmediate(p2);
            }
        }

        [Test]
        public void ElBotonDelMenu_NoUsaOnMouseDown()
        {
            // Este proyecto tiene activeInputHandler = 1, o sea SOLO el Input System nuevo.
            // Con eso Unity no envía los mensajes OnMouseXXX, así que un botón que dependa
            // de OnMouseDown queda decorativo: el usuario hace clic y no pasa nada.
            var tipo = typeof(VdfBotonPractica);
            var metodo = tipo.GetMethod("OnMouseDown",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNull(metodo,
                "OnMouseDown no se dispara con el Input System nuevo. La detección del clic " +
                "debe ir por raycast con Mouse.current, como en VdfJumperController.");
        }

        [Test]
        public void SeleccionarFueraDeRango_NoRevienta()
        {
            var menu = Crear<VdfMenuPracticas>("Menu");
            Assert.DoesNotThrow(delegate() { menu.Seleccionar(0); menu.Seleccionar(-1); menu.Seleccionar(99); });
        }

        // =====================================================================================
        // 2. Cambiar de práctica tiene que reiniciar el módulo
        // =====================================================================================

        [Test]
        public void CambiarDePractica_DeshaceLaConfiguracionCuandoLaPracticaEmpiezaDesdeFabrica()
        {
            var sim = Crear<VdfSimulacion>("Simulacion");
            sim.Despertar();   // crea Vfd y Plc sin depender de Awake

            // Como si viniéramos de una práctica que empieza configurada.
            sim.SaltarConfiguracion = true;
            sim.AplicarPuntoDePartida();
            Assert.AreEqual(40f, sim.Vfd.Valor("H1-01"), 0.01f);

            // Ahora la práctica 2, que empieza desde valores de fábrica.
            sim.SaltarConfiguracion = false;
            sim.AplicarPuntoDePartida();

            Assert.AreNotEqual(1.2f, sim.Vfd.Valor("E2-01"),
                "Si no se restauran los valores de fábrica, el alumno se encuentra el " +
                "variador ya configurado y la práctica 2 no tiene nada que hacer.");
        }

        [Test]
        public void CambiarDePractica_DeshaceElCableadoCuandoLaPracticaEmpiezaSinCablear()
        {
            var sim = Crear<VdfSimulacion>("Simulacion");
            var ctrl = Crear<VdfJumperController>("Cableado");
            ctrl.ContenedorCables = _raiz.transform;
            sim.Cableado = ctrl;
            sim.Despertar();

            // Bornes mínimos para que el cableado automático tenga dónde agarrarse.
            foreach (var red in VdfWiringSpec.Practica())
                foreach (var id in red.Bornes) Borne(id);
            ctrl.IndexarBornes();

            sim.SaltarCableado = true;
            sim.AplicarPuntoDePartida();
            Assert.Greater(ctrl.Grafo.Cuenta, 0, "Debería haber cableado automático.");

            sim.SaltarCableado = false;
            sim.AplicarPuntoDePartida();

            Assert.AreEqual(0, ctrl.Grafo.Cuenta,
                "La práctica de cableado tiene que empezar con el tablero sin cablear.");
            Assert.AreEqual(0, ctrl.CablesDibujados);
        }

        [Test]
        public void ReiniciarElPuntoDePartida_DejaElPlcEnReposo()
        {
            var sim = Crear<VdfSimulacion>("Simulacion");
            sim.Despertar();

            sim.Plc.PulsarStart();
            sim.Plc.PulsarSubir();
            sim.Plc.Tick(4f);
            Assert.AreEqual(1, sim.Plc.NivelActivo);

            sim.AplicarPuntoDePartida();

            Assert.IsFalse(sim.Plc.Habilitado, "Al cambiar de práctica el PLC vuelve a reposo.");
            Assert.AreEqual(0, sim.Plc.NivelActivo);
        }

        // =====================================================================================
        // 3. Un solo grafo de cableado, también en VR
        // =====================================================================================

        [Test]
        public void ElPuenteXR_UsaElMismoGrafoQueElControlador()
        {
            var ctrl = Crear<VdfJumperController>("Cableado");
            var puente = Crear<VdfXrWiring>("CableadoXR");
            puente.Cableado = ctrl;

            Assert.AreSame(ctrl.Grafo, puente.Grafo,
                "Con dos grafos, en VR el alumno cablea y la práctica no se entera: es " +
                "exactamente lo que pasaba antes de la fase 8.");
        }

        [Test]
        public void ResincronizarElPuenteXR_NoBorraLosCablesHechosConElRaton()
        {
            var ctrl = Crear<VdfJumperController>("Cableado");
            ctrl.ContenedorCables = _raiz.transform;
            Borne("A"); Borne("B");
            ctrl.IndexarBornes();

            var puente = Crear<VdfXrWiring>("CableadoXR");
            puente.Cableado = ctrl;

            ctrl.Conectar("A", "B");
            Assert.AreEqual(1, ctrl.Grafo.Cuenta);

            // Antes esto hacía Grafo.Limpiar(), y al compartir el grafo con el modo
            // escritorio se llevaba por delante los cables hechos con el ratón y los del
            // cableado automático de la práctica.
            puente.Resincronizar();

            Assert.AreEqual(1, ctrl.Grafo.Cuenta,
                "El puente XR solo debe retirar las aristas que puso él.");
            Assert.IsTrue(ctrl.Grafo.MismaRed("A", "B"));
        }

        // =====================================================================================
        // 4. Bornes con el blanco solapado: gana el que se esta señalando
        // =====================================================================================

        [Test]
        public void ApuntarAUnBorne_NoSeLoQuedaElVecinoAunqueEsteMasCerca()
        {
            // Reproduce la geometria real de los bornes del motor: separados 20 mm con un
            // blanco de 18 mm de radio, o sea solapados. Medido en Play Mode, apuntando al
            // centro de MOT-U se seleccionaba MOT-V.
            var u = Borne("MOT-U"); u.transform.position = new Vector3(-0.620f, 0.900f, -0.466f);
            var v = Borne("MOT-V"); v.transform.position = new Vector3(-0.600f, 0.900f, -0.466f);
            var w = Borne("MOT-W"); w.transform.position = new Vector3(-0.580f, 0.900f, -0.466f);

            var camara = new Vector3(0f, 1.620f, -1.350f);

            // Los tres los toca el rayo; la lista llega ordenada de cerca a lejos de la
            // camara, que es justo el orden que antes decidia y se equivocaba.
            var tocados = new System.Collections.Generic.List<VdfTerminal> { v, w, u };

            var rayo = new Ray(camara, (u.transform.position - camara).normalized);

            Assert.AreSame(u, VdfJumperController.ElegirBorne(rayo, tocados),
                "Apuntando a MOT-U tiene que salir MOT-U, no el vecino que queda un pelo " +
                "mas cerca de la camara.");
        }

        [Test]
        public void ApuntarAUnBorne_FuncionaParaCadaUnoDelTrioDelMotor()
        {
            var u = Borne("MOT-U"); u.transform.position = new Vector3(-0.620f, 0.900f, -0.466f);
            var v = Borne("MOT-V"); v.transform.position = new Vector3(-0.600f, 0.900f, -0.466f);
            var w = Borne("MOT-W"); w.transform.position = new Vector3(-0.580f, 0.900f, -0.466f);

            var camara = new Vector3(0f, 1.620f, -1.350f);
            var tocados = new System.Collections.Generic.List<VdfTerminal> { u, v, w };

            foreach (var esperado in tocados.ToArray())
            {
                var rayo = new Ray(camara, (esperado.transform.position - camara).normalized);
                Assert.AreSame(esperado, VdfJumperController.ElegirBorne(rayo, tocados),
                    "Los tres bornes del motor son de la practica de cableado: si uno no se " +
                    "puede señalar, esa practica no se puede terminar.");
            }
        }

        [Test]
        public void ElegirBorne_SinBornesTocados_DevuelveNulo()
        {
            var rayo = new Ray(Vector3.zero, Vector3.forward);
            Assert.IsNull(VdfJumperController.ElegirBorne(rayo, null));
            Assert.IsNull(VdfJumperController.ElegirBorne(rayo, new System.Collections.Generic.List<VdfTerminal>()));
        }

        [Test]
        public void ElegirBorne_ConDosBornesAlineadosALaVista_SeQuedaConElDeDelante()
        {
            // Aqui el criterio de antes era el correcto y tiene que seguir valiendo: si un
            // borne tapa a otro, el de delante es el que se ve y el que se clica.
            var delante = Borne("DEL"); delante.transform.position = new Vector3(0f, 0f, 1f);
            var detras = Borne("DET");  detras.transform.position = new Vector3(0f, 0f, 2f);

            var rayo = new Ray(Vector3.zero, Vector3.forward);
            var tocados = new System.Collections.Generic.List<VdfTerminal> { delante, detras };

            Assert.AreSame(delante, VdfJumperController.ElegirBorne(rayo, tocados));
        }

        [Test]
        public void ElPuenteXR_SinControladorEnLaEscena_NoRevienta()
        {
            var puente = Crear<VdfXrWiring>("CableadoXR");

            Assert.DoesNotThrow(delegate()
            {
                var g = puente.Grafo;
                Assert.IsNotNull(g);
                puente.Resincronizar();
                puente.Validar();
            });
        }
    }
}
