using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// El circuito completo de la práctica 5: provocar una avería, diagnosticarla y repararla.
    ///
    /// Estos tests existen por el hueco más grande que ha aparecido en la revisión de las
    /// ramas: **la práctica 5 nunca se podía terminar**. El catálogo de seis averías estaba
    /// escrito y probado, el evaluador sabía reconocer un diagnóstico acertado y el guion tenía
    /// su paso «di cuál crees que es la causa»… pero nadie asignaba nunca la avería activa y no
    /// había ningún sitio donde decir la causa. Los tests de entonces comprobaban las piezas
    /// por separado y todas pasaban.
    ///
    /// Por eso aquí se recorre el camino entero, con el runner y el panel de verdad.
    /// </summary>
    public class VdfDiagnosticoTests
    {
        private GameObject _raiz;
        private VdfSimulacion _sim;
        private VdfPracticaRunner _runner;
        private VdfPanelDiagnostico _panel;
        private VdfPractica _practica;

        [SetUp]
        public void SetUp()
        {
            _raiz = new GameObject("RaizDiagnostico");

            _sim = _raiz.AddComponent<VdfSimulacion>();
            Despertar(_sim);

            _runner = _raiz.AddComponent<VdfPracticaRunner>();
            _runner.Simulacion = _sim;
            Despertar(_runner);

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(_raiz.transform, false);
            _panel = panelGo.AddComponent<VdfPanelDiagnostico>();
            _panel.Runner = _runner;
            Despertar(_panel);

            _practica = VdfPracticasDefaults.CrearSeguridadYFallas();
        }

        [TearDown]
        public void TearDown()
        {
            if (_practica != null) Object.DestroyImmediate(_practica);
            if (_raiz != null) Object.DestroyImmediate(_raiz);
        }

        /// <summary>En EditMode, AddComponent no dispara Awake.</summary>
        private static void Despertar(Component c)
        {
            var awake = c.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(c, null);
        }

        private static int IndiceDe(string id)
        {
            var todas = VdfCatalogoFallas.Crear();
            for (int i = 0; i < todas.Count; i++) if (todas[i].Id == id) return i;
            Assert.Fail("No existe la avería " + id);
            return -1;
        }

        // =====================================================================================
        // La avería llega al módulo
        // =====================================================================================

        [Test]
        public void AlEmpezarLaPracticaDeFallas_ElModuloQuedaAveriado()
        {
            Assert.IsTrue(_practica.NecesitaAveria(),
                "la práctica 5 pide diagnosticar, así que necesita una avería");

            _runner.FallaForzada = "H1-01_LOCAL_REMOTE";
            _runner.Empezar(_practica);
            _runner.ProvocarAveriaAhora();   // al llegar a su paso; aquí no hay Update

            Assert.IsNotNull(_runner.Contexto.FallaActiva, "tiene que haber una avería activa");
            Assert.AreEqual("H1-01_LOCAL_REMOTE", _runner.Contexto.FallaActiva.Id);
            Assert.AreEqual(1f, _sim.Vfd.Valor("H1-01"), 0.01f,
                "y tiene que estar aplicada de verdad en el variador");
        }

        [Test]
        public void LasDemasPracticas_NoSeAverian()
        {
            var cableado = VdfPracticasDefaults.CrearCableado();
            try
            {
                Assert.IsFalse(cableado.NecesitaAveria());
                _runner.Empezar(cableado);
                Assert.IsNull(_runner.Contexto.FallaActiva,
                    "la práctica de cableado no es de diagnóstico: nada de averías sorpresa");
            }
            finally
            {
                Object.DestroyImmediate(cableado);
            }
        }

        [Test]
        public void SinIdForzado_SaleUnaDelCatalogo()
        {
            // Al azar a propósito, para que repetir la práctica no sea repetir la respuesta.
            _runner.FallaForzada = null;
            _runner.Empezar(_practica);
            _runner.ProvocarAveriaAhora();

            Assert.IsNotNull(_runner.Contexto.FallaActiva);
            Assert.IsNotNull(VdfCatalogoFallas.Buscar(_runner.Contexto.FallaActiva.Id),
                "la avería tiene que ser una de las del catálogo");
        }

        // =====================================================================================
        // El diagnóstico
        // =====================================================================================

        [Test]
        public void SenalarLaCausaCorrecta_CumpleElPasoDeDiagnostico()
        {
            _runner.FallaForzada = "b1-01_ANALOGICA";
            _runner.Empezar(_practica);
            _runner.ProvocarAveriaAhora();

            Assert.IsTrue(_panel.Senalar(IndiceDe("b1-01_ANALOGICA")), "acertó");
            Assert.IsTrue(_panel.Acertado);
            Assert.IsTrue(_runner.Contexto.Identificados.Contains("b1-01_ANALOGICA"),
                "y el runner lo da por diagnosticado");
        }

        [Test]
        public void SenalarOtraCausa_NoVale_YSePuedeSeguirIntentando()
        {
            _runner.FallaForzada = "b1-01_ANALOGICA";
            _runner.Empezar(_practica);
            _runner.ProvocarAveriaAhora();

            Assert.IsFalse(_panel.Senalar(IndiceDe("V3_SIN_CABLE")), "no es esa");
            Assert.AreEqual(1, _panel.Fallos, "el intento fallido se cuenta");
            Assert.IsFalse(_panel.Acertado);
            Assert.IsFalse(_runner.Contexto.Identificados.Contains("b1-01_ANALOGICA"),
                "y desde luego no cuenta como diagnosticada");

            // Equivocarse no bloquea: se sigue pudiendo acertar.
            Assert.IsTrue(_panel.Senalar(IndiceDe("b1-01_ANALOGICA")));
            Assert.IsTrue(_panel.Acertado);
        }

        [Test]
        public void SinAveriaActiva_SenalarNoDaPorBuenoNada()
        {
            // Es el estado en el que estaba el simulador entero antes de esta revisión.
            Assert.IsFalse(_panel.Senalar(0));
            Assert.IsFalse(_panel.Acertado);
            Assert.AreEqual(0, _panel.Fallos, "sin avería no es un fallo del alumno");
        }

        // =====================================================================================
        // La práctica entera
        // =====================================================================================

        [Test]
        public void LaPracticaDeFallas_SePuedeTerminar()
        {
            // El runner solo aplica el punto de partida en Play, así que aquí se deja el
            // módulo como se lo encontraría el alumno: configurado y cableado, que es con lo
            // que arranca la práctica 5. Luego Empezar provoca la avería encima.
            _sim.SaltarConfiguracion = true;
            _sim.SaltarCableado = true;
            _sim.AplicarPuntoDePartida();

            _runner.FallaForzada = "b1-01_ANALOGICA";
            _runner.Empezar(_practica);

            Assert.Less(Cumplidos(), _runner.TotalPasos, "al empezar no está hecha");

            // Lo que haría el alumno, en orden.
            Confirmar();                                   // los pasos de lectura
            _sim.PulsarStart();
            _sim.Plc.Tick(0.5f);
            Refrescar();

            _sim.PulsarEmergencia();                       // la parte de seguridad
            _sim.Plc.Tick(0.5f);
            _runner.Contexto.EmergenciaConMotorGirando = true;   // sin rampa de motor aquí
            Refrescar();
            _sim.Rearmar();
            _sim.Plc.Tick(0.5f);
            Refrescar();
            _runner.ProvocarAveriaAhora();                 // llega con el paso «a partir de aquí»
            Confirmar();

            _panel.Senalar(IndiceDe("b1-01_ANALOGICA"));   // el diagnóstico
            Refrescar();

            _sim.Vfd.EscribirParametro("b1-01", 0f);       // la reparación
            _runner.Contexto.NivelMaximoTrasReparar = PlcFanLogic.NivelMaximo;  // y la prueba
            Refrescar();
            Confirmar();

            Assert.AreEqual(_runner.TotalPasos, Cumplidos(), SinCumplir());
        }

        private int Cumplidos()
        {
            int n = 0;
            for (int i = 0; i < _runner.TotalPasos; i++) if (_runner.PasoCumplido(i)) n++;
            return n;
        }

        private string SinCumplir()
        {
            var sb = new System.Text.StringBuilder("Pasos sin cumplir: ");
            for (int i = 0; i < _runner.TotalPasos; i++)
                if (!_runner.PasoCumplido(i)) sb.Append("\n  - " + _runner.Practica.Paso(i).Instruccion);
            return sb.ToString();
        }

        private void Confirmar()
        {
            while (_runner.ConfirmarPasoManual()) { }
        }

        /// <summary>
        /// Lo que hace el Update del runner, que en EditMode no corre: volcar el estado de la
        /// simulación al contexto y apuntar los logros.
        /// </summary>
        private void Refrescar()
        {
            _runner.Contexto.Vfd = _sim.Vfd;
            _runner.Contexto.Plc = _sim.Plc;
            _runner.Contexto.ActualizarLogros();
        }
    }
}
