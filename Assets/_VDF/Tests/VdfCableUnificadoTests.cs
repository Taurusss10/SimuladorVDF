using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    public class VdfCableUnificadoTests
    {
        private GameObject _raiz;
        private VdfJumperController _control;

        [SetUp]
        public void SetUp()
        {
            _raiz = new GameObject("RaizPrueba");

            var contenedor = new GameObject("Cables");
            contenedor.transform.SetParent(_raiz.transform, false);

            var go = new GameObject("Control");
            go.transform.SetParent(_raiz.transform, false);
            _control = go.AddComponent<VdfJumperController>();
            _control.ContenedorCables = contenedor.transform;

            CrearBorne("A", new Vector3(0f, 1f, 0f));
            CrearBorne("B", new Vector3(0.3f, 1f, 0f));
            CrearBorne("C", new Vector3(0.6f, 1f, 0f));

            _control.IndexarBornes();
        }

        [TearDown]
        public void TearDown()
        {
            if (_raiz != null) Object.DestroyImmediate(_raiz);
        }

        private VdfTerminal CrearBorne(string id, Vector3 posicion)
        {
            var go = new GameObject("Borne_" + id);
            go.transform.SetParent(_raiz.transform, false);
            go.transform.localPosition = posicion;

            var t = go.AddComponent<VdfTerminal>();
            t.Id = id;
            t.Etiqueta = id;
            return t;
        }

        private VdfCablePhysics PrimerCable()
        {
            return _control.ContenedorCables.GetComponentInChildren<VdfCablePhysics>();
        }

        // =====================================================================================
        // Lo que se ve y lo que se valida son lo mismo
        // =====================================================================================

        [Test]
        public void Conectar_CreaUnCableConFisica_NoUnaParabola()
        {
            Assert.IsTrue(_control.Conectar("A", "B"));

            var cable = PrimerCable();
            Assert.IsNotNull(cable,
                "El cable que se crea debe ser el de física: es el único sistema desde la fase 4.");
        }

        [Test]
        public void ElCableConFisica_SabeQueBornesUne()
        {
            _control.Conectar("A", "B");
            var cable = PrimerCable();

            Assert.AreEqual("A", cable.IdA);
            Assert.AreEqual("B", cable.IdB);
            Assert.IsTrue(cable.Toca("A"));
            Assert.IsTrue(cable.Toca("B"));
            Assert.IsFalse(cable.Toca("C"));
            Assert.AreEqual("B", cable.Otro("A"));
            Assert.IsTrue(cable.Une("B", "A"), "Une debe ser simétrica.");
        }

        [Test]
        public void Conectar_DejaElGrafoYLaEscenaDeAcuerdo()
        {
            _control.Conectar("A", "B");
            _control.Conectar("B", "C");

            Assert.AreEqual(2, _control.Grafo.Cuenta, "El grafo debe tener las dos aristas.");
            Assert.AreEqual(2, _control.CablesDibujados, "Y la escena los dos cables.");
            Assert.AreEqual(2, _control.ContenedorCables.GetComponentsInChildren<VdfCablePhysics>().Length);
            Assert.IsTrue(_control.Grafo.MismaRed("A", "C"), "A y C quedan al mismo potencial.");
        }

        [Test]
        public void BorrarCable_QuitaLaAristaYElObjetoALaVez()
        {
            _control.Conectar("A", "B");
            var cable = PrimerCable();

            _control.BorrarCable(cable);

            Assert.AreEqual(0, _control.Grafo.Cuenta, "La arista debe desaparecer del grafo.");
            Assert.AreEqual(0, _control.CablesDibujados);
            Assert.IsFalse(_control.Grafo.Existe("A", "B"));
        }

        [Test]
        public void BorrarCablesDeUnBorne_QuitaTodosLosSuyos()
        {
            _control.Conectar("A", "B");
            _control.Conectar("B", "C");

            int quitados = _control.BorrarCablesDe("B");

            Assert.AreEqual(2, quitados);
            Assert.AreEqual(0, _control.Grafo.Cuenta);
            Assert.AreEqual(0, _control.CablesDibujados);
        }

        [Test]
        public void UnCableDuplicado_NiSeDibujaNiEntraDosVecesEnElGrafo()
        {
            Assert.IsTrue(_control.Conectar("A", "B"));
            Assert.IsFalse(_control.Conectar("B", "A"), "Es el mismo cable, en el otro orden.");

            Assert.AreEqual(1, _control.Grafo.Cuenta);
            Assert.AreEqual(1, _control.CablesDibujados,
                "Si el grafo lo rechaza, tampoco debe quedar un cable huérfano en la escena.");
        }

        [Test]
        public void ConectarConUnBorneInexistente_NoTocaElGrafo()
        {
            Assert.IsFalse(_control.Conectar("A", "NO_EXISTE"));
            Assert.AreEqual(0, _control.Grafo.Cuenta);
            Assert.AreEqual(0, _control.CablesDibujados);
        }

        // =====================================================================================
        // El volumen de selección sigue la curva
        // =====================================================================================

        [Test]
        public void ElCable_TieneVariasCapsulasParaPoderSeleccionarlo()
        {
            _control.Conectar("A", "B");
            var cable = PrimerCable();

            var capsulas = cable.GetComponentsInChildren<CapsuleCollider>();

            Assert.GreaterOrEqual(capsulas.Length, 2,
                "Hacen falta varias cápsulas para que la selección siga la curva.");
            Assert.AreEqual(cable.SegmentosCollider, capsulas.Length);
        }

        [Test]
        public void LasCapsulasDeSeleccion_SonTrigger()
        {
            _control.Conectar("A", "B");
            var cable = PrimerCable();

            foreach (var c in cable.GetComponentsInChildren<CapsuleCollider>())
            {
                Assert.IsTrue(c.isTrigger,
                    "El volumen de selección no debe empujar nada ni participar en la física.");
            }
        }

        [Test]
        public void LasCapsulas_SonMasGruesasQueElCable()
        {
            _control.Conectar("A", "B");
            var cable = PrimerCable();

            foreach (var c in cable.GetComponentsInChildren<CapsuleCollider>())
            {
                Assert.Greater(c.radius, cable.Grosor * 0.5f,
                    "Sin holgura, acertarle a un cable de 6 mm exige demasiada puntería, " +
                    "y con el rayo de un mando en VR es peor.");
            }
        }

        // =====================================================================================
        // La validación ve lo que hay en la escena
        // =====================================================================================

        [Test]
        public void CablearCorrectamente_ProduceUnInformeSinFaltantes()
        {
            // Esta escena de prueba no tiene los bornes de la práctica, así que se comprueba
            // lo contrario: que la validación SÍ se queja, y que lo hace mirando el grafo
            // que los cables de física acaban de llenar.
            _control.Conectar("A", "B");

            var informe = _control.Validar();

            Assert.IsFalse(informe.EsCorrecto);
            Assert.AreEqual(1, informe.CablesPuestos,
                "El informe cuenta los cables del grafo, que ahora son los de física.");
        }

        // =====================================================================================
        // Las formas de colisión salen de la geometría real
        // =====================================================================================

        [Test]
        public void Limites_IncluyenElTamanoDelPrimerRenderer()
        {
            // Una caja de 2 x 0,04 x 0,8 centrada en y = 0,73: su cara superior está en 0,75.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = new Vector3(0f, 0.73f, 0f);
                go.transform.localScale = new Vector3(2f, 0.04f, 0.8f);

                Bounds b;
                Assert.IsTrue(VdfColisionBuilder.Limites(go.transform, out b));

                // El fallo que cubre este test: partir de unos límites de tamaño cero en el
                // centro del primer renderer y encapsular solo a partir del segundo. Con un
                // único renderer, las dimensiones se perdían enteras y la cara superior
                // salía en 0,73 en vez de 0,75, dejando el plano donde se apoyan los cables
                // dos centímetros por debajo de la mesa.
                Assert.AreEqual(0.75f, b.max.y, 0.0001f,
                    "La cara superior debe salir de los límites completos, no del centro.");
                Assert.AreEqual(2f, b.size.x, 0.0001f);
                Assert.Greater(b.extents.y, 0f, "Unos límites de tamaño cero no sirven.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Limites_EncapsulanTodosLosHijos()
        {
            var padre = new GameObject("Padre");
            try
            {
                var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
                a.transform.SetParent(padre.transform, false);
                a.transform.localPosition = Vector3.zero;

                var b2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b2.transform.SetParent(padre.transform, false);
                b2.transform.localPosition = new Vector3(3f, 0f, 0f);

                Bounds b;
                Assert.IsTrue(VdfColisionBuilder.Limites(padre.transform, out b));

                Assert.AreEqual(-0.5f, b.min.x, 0.0001f);
                Assert.AreEqual(3.5f, b.max.x, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(padre);
            }
        }

        [Test]
        public void Limites_DevuelveFalseSiNoHayRenderers()
        {
            var go = new GameObject("Vacio");
            try
            {
                Bounds b;
                Assert.IsFalse(VdfColisionBuilder.Limites(go.transform, out b));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // =====================================================================================
        // Señalar un borne: identificar frente a cablear
        // =====================================================================================

        [Test]
        public void SenalarUnBorne_AvisaAQuienEscuche()
        {
            string ultimo = null;
            _control.BorneSenalado += delegate(VdfTerminal t) { ultimo = t != null ? t.Id : null; };

            _control.SenalarBorne(_control.BuscarBorne("A"));

            Assert.AreEqual("A", ultimo,
                "El aviso es lo que permite que la práctica 1 cuente el borne como identificado.");
        }

        [Test]
        public void SinPermisoParaCablear_SenalarBornesNoCreaCables()
        {
            _control.PermitirCablear = false;

            _control.SenalarBorne(_control.BuscarBorne("A"));
            _control.SenalarBorne(_control.BuscarBorne("B"));

            // En la práctica de reconocimiento el alumno va tocando componentes para
            // identificarlos; no debería acabar con el tablero lleno de cables sin querer.
            Assert.AreEqual(0, _control.Grafo.Cuenta);
            Assert.AreEqual(0, _control.CablesDibujados);
            Assert.IsNull(_control.Seleccionado,
                "Tampoco debe quedar un borne a medio cablear.");
        }

        [Test]
        public void SinPermisoParaCablear_ElAvisoSigueLlegando()
        {
            _control.PermitirCablear = false;

            int avisos = 0;
            _control.BorneSenalado += delegate(VdfTerminal t) { avisos++; };

            _control.SenalarBorne(_control.BuscarBorne("A"));
            _control.SenalarBorne(_control.BuscarBorne("B"));

            Assert.AreEqual(2, avisos,
                "Sin cablear, señalar sigue siendo la forma de identificar componentes.");
        }

        [Test]
        public void ConPermisoParaCablear_DosBornesSenaladosFormanUnCable()
        {
            _control.PermitirCablear = true;

            _control.SenalarBorne(_control.BuscarBorne("A"));
            Assert.IsNotNull(_control.Seleccionado, "El primero queda a la espera del segundo.");

            _control.SenalarBorne(_control.BuscarBorne("B"));

            Assert.AreEqual(1, _control.Grafo.Cuenta);
            Assert.AreEqual(1, _control.CablesDibujados);
            Assert.IsNull(_control.Seleccionado);
        }

        [Test]
        public void SenalarDosVecesElMismoBorne_LoDeselecciona()
        {
            _control.SenalarBorne(_control.BuscarBorne("A"));
            _control.SenalarBorne(_control.BuscarBorne("A"));

            Assert.IsNull(_control.Seleccionado);
            Assert.AreEqual(0, _control.Grafo.Cuenta, "No debe crearse un cable consigo mismo.");
        }

        [Test]
        public void BorrarTodo_DejaGrafoYEscenaVacios()
        {
            _control.Conectar("A", "B");
            _control.Conectar("B", "C");

            _control.BorrarTodo();

            Assert.AreEqual(0, _control.Grafo.Cuenta);
            Assert.AreEqual(0, _control.CablesDibujados);
            Assert.AreEqual(0, _control.ContenedorCables.GetComponentsInChildren<VdfCablePhysics>().Length);
        }
    }
}
