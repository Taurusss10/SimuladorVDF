using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// E1: pruebas del modelo del variador con tiempo simulado.
    /// Nada de esto necesita Play Mode ni escena: VfdModel es C# puro.
    /// </summary>
    public class VfdModelTests
    {
        private VfdModel _vfd;

        /// <summary>Avanza el modelo en pasos pequeños, como haría un FixedUpdate a 50 Hz.</summary>
        private void Avanzar(float segundos, float paso = 0.02f)
        {
            float t = 0f;
            while (t < segundos)
            {
                float dt = UnityEngine.Mathf.Min(paso, segundos - t);
                _vfd.Tick(dt);
                t += dt;
            }
        }

        /// <summary>Configura el variador y el cableado correctos de la práctica.</summary>
        private void ConfigurarCorrectamente()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();
        }

        [SetUp]
        public void SetUp()
        {
            _vfd = new VfdModel();
        }

        // =================================================================================
        // Tabla de parámetros
        // =================================================================================

        [Test]
        public void ValoresDeFabrica_SeCarganParaTodosLosParametros()
        {
            foreach (var def in VfdParameterTable.Todos)
                Assert.AreEqual(def.PorDefecto, _vfd.Valor(def.Id), 0.001f, "Parametro " + def.Id);
        }

        [Test]
        public void EscribirParametro_RechazaValorFueraDeRango()
        {
            string error;
            bool ok = _vfd.EscribirParametro("E1-04", 999f, out error);

            Assert.IsFalse(ok, "E1-04 = 999 Hz deberia rechazarse");
            Assert.IsNotNull(error);
            Assert.AreEqual(60.0f, _vfd.Valor("E1-04"), 0.001f, "El valor no debe cambiar si se rechaza");
        }

        [Test]
        public void EscribirParametro_RechazaValorNoEnumerado()
        {
            string error;
            Assert.IsFalse(_vfd.EscribirParametro("L1-01", 7f, out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void EscribirParametro_CuantizaALaResolucionDelParametro()
        {
            // C1-01 tiene 1 decimal: 10.37 s debe quedar en 10.4 s.
            Assert.IsTrue(_vfd.EscribirParametro("C1-01", 10.37f));
            Assert.AreEqual(10.4f, _vfd.Valor("C1-01"), 0.001f);
        }

        [Test]
        public void A1_03_2220_InicializaA2Hilos_YVuelveACero()
        {
            _vfd.EscribirParametro("H1-01", VfdTerminalFunction.LocalRemote);
            Assert.IsTrue(_vfd.EscribirParametro("A1-03", 2220));

            Assert.AreEqual(VfdTerminalFunction.MarchaAdelante2Hilos, _vfd.ValorEntero("H1-01"),
                "Tras inicializar a 2 hilos, S1 debe ser marcha adelante (40)");
            Assert.AreEqual(VfdTerminalFunction.MarchaAtras2Hilos, _vfd.ValorEntero("H1-02"));
            Assert.AreEqual(0, _vfd.ValorEntero("A1-03"),
                "A1-03 vuelve a 0 despues de ejecutar la inicializacion");
        }

        // =================================================================================
        // Selección de velocidad (tabla del manual oficial)
        // =================================================================================

        [Test]
        public void SeleccionDeVelocidad_SigueLaTablaDelManualOficial()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true); // marcha

            // Ninguna entrada -> d1-01 = 10 Hz
            _vfd.SetTerminal(5, false);
            _vfd.SetTerminal(6, false);
            Assert.AreEqual(10.0f, _vfd.ResolverReferenciaHz(), 0.01f, "sin S5 ni S6 -> d1-01");

            // Solo S5 -> d1-02 = 25 Hz
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, false);
            Assert.AreEqual(25.0f, _vfd.ResolverReferenciaHz(), 0.01f, "solo S5 -> d1-02");

            // Solo S6 -> d1-03 = 35 Hz
            _vfd.SetTerminal(5, false);
            _vfd.SetTerminal(6, true);
            Assert.AreEqual(35.0f, _vfd.ResolverReferenciaHz(), 0.01f, "solo S6 -> d1-03");

            // S5 y S6 -> d1-04 = 50 Hz
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, true);
            Assert.AreEqual(50.0f, _vfd.ResolverReferenciaHz(), 0.01f, "S5 + S6 -> d1-04");
        }

        [Test]
        public void ReferenciaSeRecortaALaFrecuenciaMaximaE1_04()
        {
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("E1-04", 40.0f); // por debajo de d1-04 = 50 Hz
            _vfd.SetTerminal(1, true);
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, true);

            Assert.AreEqual(40.0f, _vfd.ResolverReferenciaHz(), 0.01f,
                "d1-04 = 50 Hz debe recortarse al maximo E1-04 = 40 Hz");
        }

        // =================================================================================
        // Marcha, rampas y estados
        // =================================================================================

        [Test]
        public void SinOrdenDeMarcha_ElMotorNoGira()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, false);

            Avanzar(5f);

            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
            Assert.AreEqual(VfdState.Detenido, _vfd.Estado);
        }

        [Test]
        public void Arranque_PasaPorAcelerandoYLlegaAEnMarcha()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true); // 10 Hz (d1-01)

            _vfd.Tick(0.1f);
            Assert.AreEqual(VfdState.Acelerando, _vfd.Estado, "justo tras arrancar debe estar acelerando");
            Assert.Greater(_vfd.FrecuenciaSalidaHz, 0f);

            Avanzar(5f);
            Assert.AreEqual(VfdState.EnMarcha, _vfd.Estado);
            Assert.AreEqual(10.0f, _vfd.FrecuenciaSalidaHz, 0.05f);
        }

        [Test]
        public void RampaDeAceleracion_UsaC1_01ComoTiempoDeCeroAE1_04()
        {
            ConfigurarCorrectamente();
            // C1-01 = 10 s para ir de 0 a E1-04 = 60 Hz -> 6 Hz/s.
            // Llegar a d1-04 = 50 Hz debe costar 50/6 = 8.33 s.
            _vfd.SetTerminal(1, true);
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, true);

            Avanzar(5f);
            Assert.AreEqual(30.0f, _vfd.FrecuenciaSalidaHz, 0.3f,
                "a los 5 s con 6 Hz/s deberia ir por 30 Hz");
            Assert.AreEqual(VfdState.Acelerando, _vfd.Estado);

            Avanzar(3.4f);
            Assert.AreEqual(50.0f, _vfd.FrecuenciaSalidaHz, 0.3f, "a los 8.4 s debe haber llegado a 50 Hz");
            Assert.AreEqual(VfdState.EnMarcha, _vfd.Estado);
        }

        [Test]
        public void RampaDeDesaceleracion_UsaC1_02()
        {
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("C1-02", 5.0f); // 60 Hz / 5 s = 12 Hz/s
            _vfd.SetTerminal(1, true);
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, true);
            Avanzar(20f);
            Assert.AreEqual(50.0f, _vfd.FrecuenciaSalidaHz, 0.05f);

            _vfd.SetTerminal(1, false); // orden de parada
            _vfd.Tick(0.1f);
            Assert.AreEqual(VfdState.Desacelerando, _vfd.Estado);

            Avanzar(1f);
            // 1.1 s a 12 Hz/s -> ~13.2 Hz menos
            Assert.AreEqual(36.8f, _vfd.FrecuenciaSalidaHz, 0.5f);

            Avanzar(5f);
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
            Assert.AreEqual(VfdState.Detenido, _vfd.Estado);
        }

        [Test]
        public void CambioDeVelocidadEnMarcha_RampaHastaLaNuevaReferencia()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true);
            Avanzar(10f);
            Assert.AreEqual(10.0f, _vfd.FrecuenciaSalidaHz, 0.05f, "velocidad 1");

            _vfd.SetTerminal(5, true); // sube a velocidad 2 = 25 Hz
            _vfd.Tick(0.1f);
            Assert.AreEqual(VfdState.Acelerando, _vfd.Estado);

            Avanzar(10f);
            Assert.AreEqual(25.0f, _vfd.FrecuenciaSalidaHz, 0.05f, "velocidad 2");
        }

        [Test]
        public void AdelanteYAtrasALaVez_SeInterpretaComoParada()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true);
            _vfd.SetTerminal(2, true); // H1-02 = 41, marcha atras

            Avanzar(5f);

            Assert.IsFalse(_vfd.OrdenDeMarchaActiva());
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
        }

        // =================================================================================
        // Alimentación, fallas y emergencia
        // =================================================================================

        [Test]
        public void SinAlimentacion_NoArranca()
        {
            ConfigurarCorrectamente();
            _vfd.SetAlimentacion(false);
            _vfd.SetTerminal(1, true);

            Avanzar(5f);

            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
            Assert.IsFalse(_vfd.LedDrv, "el LED DRV refleja que el variador no esta energizado");
        }

        [Test]
        public void ParadaEmergencia_CortaDeInmediatoYEnciendeALM()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true);
            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, true);
            Avanzar(20f);
            Assert.AreEqual(50.0f, _vfd.FrecuenciaSalidaHz, 0.05f);

            _vfd.ParadaEmergencia();

            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.001f, "PE corta sin rampa");
            Assert.AreEqual(VfdState.Falla, _vfd.Estado);
            Assert.IsTrue(_vfd.LedAlm);
        }

        [Test]
        public void EnFalla_NoVuelveAArrancarHastaResetear()
        {
            ConfigurarCorrectamente();
            _vfd.SetTerminal(1, true);
            _vfd.ParadaEmergencia();

            Avanzar(10f);
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f, "con falla activa no arranca aunque S1 siga activo");

            Assert.IsTrue(_vfd.ResetFalla());
            Assert.AreEqual(VfdFault.Ninguna, _vfd.Falla);

            Avanzar(10f);
            Assert.AreEqual(10.0f, _vfd.FrecuenciaSalidaHz, 0.05f, "tras el reset vuelve a operar");
        }

        [Test]
        public void ResetFalla_FallaSiLaCausaSigueActiva()
        {
            _vfd.SetAlimentacion(false);
            _vfd.DispararFalla(VfdFault.BajoVoltaje);

            Assert.IsFalse(_vfd.ResetFalla(), "no se puede resetear Uv1 sin alimentacion");
            Assert.AreEqual(VfdFault.BajoVoltaje, _vfd.Falla);

            _vfd.SetAlimentacion(true);
            Assert.IsTrue(_vfd.ResetFalla());
        }

        // =================================================================================
        // Detección de errores de configuración (práctica 5)
        // =================================================================================

        [Test]
        public void ConfiguracionDeReferencia_NoProduceProblemas()
        {
            ConfigurarCorrectamente();

            var problemas = _vfd.ValidarConfiguracion();

            Assert.AreEqual(0, problemas.Count,
                "La configuracion correcta no deberia generar avisos. Se obtuvo: " +
                string.Join(" | ", problemas.ConvertAll(p => p.ToString()).ToArray()));
            Assert.IsTrue(_vfd.ConfiguracionEsOperable());
        }

        [Test]
        public void H1_01_IgualA1_ImpideQueS1ArranqueElMotor()
        {
            // Este es exactamente el error del manual del modulo.
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("H1-01", VfdTerminalFunction.LocalRemote); // 1 = LOCAL/REMOTE

            _vfd.SetTerminal(1, true);
            Avanzar(15f);

            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f,
                "Con H1-01 = 1, S1 selecciona LOCAL/REMOTE y no arranca el motor");
            Assert.IsFalse(_vfd.OrdenDeMarchaActiva());
            Assert.IsFalse(_vfd.ConfiguracionEsOperable());

            var problemas = _vfd.ValidarConfiguracion();
            Assert.IsTrue(problemas.Exists(p => p.ParametroId == "H1-01" && p.Severidad == VfdIssueSeverity.Critica),
                "Debe reportarse como problema critico de H1-01");
        }

        [Test]
        public void H1_05_Y_H1_06_Invertidos_IntercambianLasVelocidades2Y3()
        {
            // El otro error del manual del modulo: la tabla S5/S6 esta al reves.
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("H1-05", VfdTerminalFunction.MultiVelocidad2); // 4
            _vfd.EscribirParametro("H1-06", VfdTerminalFunction.MultiVelocidad1); // 3

            _vfd.SetTerminal(1, true);

            _vfd.SetTerminal(5, true);
            _vfd.SetTerminal(6, false);
            Assert.AreEqual(35.0f, _vfd.ResolverReferenciaHz(), 0.01f,
                "Con H1-05/H1-06 invertidos, solo S5 da d1-03 (35 Hz) en vez de d1-02 (25 Hz)");

            _vfd.SetTerminal(5, false);
            _vfd.SetTerminal(6, true);
            Assert.AreEqual(25.0f, _vfd.ResolverReferenciaHz(), 0.01f, "y solo S6 da d1-02");

            // El motor si arranca: es un error de comportamiento, no critico.
            Assert.IsTrue(_vfd.ConfiguracionEsOperable());
            var problemas = _vfd.ValidarConfiguracion();
            Assert.IsTrue(problemas.Exists(p => p.Severidad == VfdIssueSeverity.Advertencia && p.ParametroId == "H1-05"));
        }

        [Test]
        public void b1_01_IgualA1_DejaLaVelocidad1EnCeroPeroNoLasDemas()
        {
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("b1-01", 1f); // referencia desde la entrada analogica A1
            _vfd.ReferenciaAnalogicaHz = 0f;     // A1 sin cablear

            _vfd.SetTerminal(1, true);

            Assert.AreEqual(0f, _vfd.ResolverReferenciaHz(), 0.01f, "velocidad 1 se queda sin referencia");

            _vfd.SetTerminal(5, true);
            Assert.AreEqual(25.0f, _vfd.ResolverReferenciaHz(), 0.01f, "las multi-velocidad si funcionan");

            var problemas = _vfd.ValidarConfiguracion();
            Assert.IsTrue(problemas.Exists(p => p.ParametroId == "b1-01"));
        }

        [Test]
        public void b1_02_DistintoDe1_EsCritico()
        {
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("b1-02", 0f); // marcha desde el operador

            _vfd.SetTerminal(1, true);
            Avanzar(15f);

            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
            Assert.IsFalse(_vfd.ConfiguracionEsOperable());
        }

        [Test]
        public void L1_01_IgualA0_SeReportaComoRiesgoDeSeguridad()
        {
            ConfigurarCorrectamente();
            _vfd.EscribirParametro("L1-01", 0f);

            var problemas = _vfd.ValidarConfiguracion();

            Assert.IsTrue(problemas.Exists(p => p.ParametroId == "L1-01" && p.Severidad == VfdIssueSeverity.Advertencia));
            Assert.IsTrue(_vfd.ConfiguracionEsOperable(), "es un aviso, no impide operar");
        }

        [Test]
        public void ValoresDeFabrica_ProducenUnaConfiguracionNoOperable()
        {
            // Sin configurar nada, d1-01..04 estan a 0 y b1-01 = 1: la practica no sale.
            var problemas = _vfd.ValidarConfiguracion();

            Assert.Greater(problemas.Count, 0, "la configuracion de fabrica no es la de la practica");
            _vfd.SetTerminal(1, true);
            Avanzar(15f);
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f,
                "de fabrica, d1-01 = 0 Hz: el motor no gira aunque reciba la orden de marcha");
        }
    }
}
