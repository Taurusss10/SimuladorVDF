using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// E2: pruebas de la lógica del PLC con tiempo simulado.
    /// El retardo de 3 s se verifica mirando justo antes y justo después del vencimiento.
    /// </summary>
    public class PlcFanLogicTests
    {
        private PlcFanLogic _plc;

        private void Avanzar(float segundos, float paso = 0.05f)
        {
            float t = 0f;
            while (t < segundos)
            {
                float dt = UnityEngine.Mathf.Min(paso, segundos - t);
                _plc.Tick(dt);
                t += dt;
            }
        }

        /// <summary>Pulsa subir y deja vencer el retardo.</summary>
        private void SubirYEsperar()
        {
            _plc.PulsarSubir();
            Avanzar(PlcFanLogic.RetardoCambioNivelSegundos + 0.2f);
        }

        private void BajarYEsperar()
        {
            _plc.PulsarBajar();
            Avanzar(PlcFanLogic.RetardoCambioNivelSegundos + 0.2f);
        }

        [SetUp]
        public void SetUp()
        {
            _plc = new PlcFanLogic();
        }

        // =================================================================================
        // Estado inicial y habilitación
        // =================================================================================

        [Test]
        public void ArrancaEnReposoSinVelocidadSeleccionada()
        {
            Assert.IsFalse(_plc.Habilitado);
            Assert.AreEqual(0, _plc.NivelActivo);
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsFalse(_plc.SalidaS6);
            for (int v = 1; v <= 4; v++)
                Assert.IsFalse(_plc.SalidaV(v), "V" + v + " no debe estar encendido en reposo");
        }

        [Test]
        public void SubirSinStart_NoHaceNada()
        {
            SubirYEsperar();

            Assert.AreEqual(0, _plc.NivelActivo);
            Assert.IsFalse(_plc.SalidaS1);
        }

        [Test]
        public void Start_HabilitaPeroNoArrancaElMotor()
        {
            _plc.PulsarStart();
            Avanzar(5f);

            Assert.IsTrue(_plc.Habilitado);
            Assert.AreEqual(0, _plc.NivelActivo, "START solo habilita; el motor arranca al subir velocidad");
            Assert.IsFalse(_plc.SalidaS1);
        }

        // =================================================================================
        // Retardo de 3 s
        // =================================================================================

        [Test]
        public void CambioDeNivel_EsperaTresSegundosAntesDeAplicarse()
        {
            _plc.PulsarStart();
            _plc.PulsarSubir();

            Assert.AreEqual(1, _plc.NivelPendiente, "el nivel pedido se registra de inmediato");
            Assert.AreEqual(0, _plc.NivelActivo, "pero no se aplica todavia");
            Assert.IsTrue(_plc.CambioEnCurso);

            Avanzar(2.8f);
            Assert.AreEqual(0, _plc.NivelActivo, "a los 2,8 s aun no ha vencido el retardo");
            Assert.IsFalse(_plc.SalidaS1);

            Avanzar(0.4f);
            Assert.AreEqual(1, _plc.NivelActivo, "a los 3,2 s ya debe estar aplicado");
            Assert.IsTrue(_plc.SalidaS1);
            Assert.IsFalse(_plc.CambioEnCurso);
        }

        [Test]
        public void TiempoRestanteCambio_CuentaAtrasYLlegaACero()
        {
            _plc.PulsarStart();
            _plc.PulsarSubir();
            Assert.AreEqual(3f, _plc.TiempoRestanteCambio, 0.01f);

            Avanzar(1f);
            Assert.AreEqual(2f, _plc.TiempoRestanteCambio, 0.1f);

            Avanzar(2.5f);
            Assert.AreEqual(0f, _plc.TiempoRestanteCambio, 0.01f);
        }

        // =================================================================================
        // Subir y bajar niveles
        // =================================================================================

        [Test]
        public void Subir_AvanzaUnNivelPorPulsacionHastaCuatro()
        {
            _plc.PulsarStart();

            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo);
            SubirYEsperar();
            Assert.AreEqual(2, _plc.NivelActivo);
            SubirYEsperar();
            Assert.AreEqual(3, _plc.NivelActivo);
            SubirYEsperar();
            Assert.AreEqual(4, _plc.NivelActivo);

            SubirYEsperar();
            Assert.AreEqual(4, _plc.NivelActivo, "no debe pasar de la velocidad 4");
        }

        [Test]
        public void Bajar_RetrocedeUnNivelPorPulsacion()
        {
            _plc.PulsarStart();
            for (int i = 0; i < 4; i++) SubirYEsperar();
            Assert.AreEqual(4, _plc.NivelActivo);

            BajarYEsperar();
            Assert.AreEqual(3, _plc.NivelActivo);
            BajarYEsperar();
            Assert.AreEqual(2, _plc.NivelActivo);
            BajarYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo);
        }

        [Test]
        public void Bajar_EnNivel1_ParaElMotorPorDefecto()
        {
            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo);

            BajarYEsperar();

            Assert.AreEqual(0, _plc.NivelActivo,
                "el Ladder original lleva la cuenta a 0 y el motor se para");
            Assert.IsFalse(_plc.SalidaS1);
        }

        /// <summary>
        /// La variante contraria sigue existiendo como opcion, por si algun dia se quiere
        /// que el nivel 1 sea un suelo y que parar sea cosa exclusiva del STOP.
        /// </summary>
        [Test]
        public void Bajar_EnNivel1_SeMantieneSiSeConfiguraAsi()
        {
            _plc.BajarDesdeNivel1Para = false;
            _plc.PulsarStart();
            SubirYEsperar();

            BajarYEsperar();

            Assert.AreEqual(1, _plc.NivelActivo);
            Assert.IsTrue(_plc.SalidaS1);
        }

        /// <summary>
        /// Lo que hace el motor MIENTRAS espera: pararse.
        ///
        /// En el Ladder original cada velocidad sale de (CUENTA == n) --[TON T#3S]--> y el
        /// motor es la suma de las cuatro. Al cambiar la cuenta, la comparacion del nivel
        /// viejo deja de cumplirse y su VELOCIDAD_n cae en el acto, mientras que la del
        /// nivel nuevo tarda los 3 s en subir. En medio no hay ninguna activa.
        ///
        /// Antes el simulador mantenia la velocidad anterior durante la espera, que era el
        /// supuesto anotado en el codigo. El docente lo narra en el video del laboratorio:
        /// "ahi frena, vuelve y arranca".
        /// </summary>
        [Test]
        public void CambiarDeNivel_ParaElMotorDuranteLaEspera()
        {
            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo, "deberia estar girando en la velocidad 1");
            Assert.IsTrue(_plc.SalidaS1);

            _plc.PulsarSubir();

            // Nada mas pulsar, antes incluso de que corra el reloj.
            Assert.AreEqual(0, _plc.NivelActivo, "el motor deberia caer en el acto");
            Assert.IsFalse(_plc.SalidaS1, "la orden de marcha deberia caer con el");
            Assert.IsFalse(_plc.SalidaV(1), "y el indicador de la velocidad vieja apagarse");
            Assert.AreEqual(2, _plc.NivelPendiente, "pero el nivel pedido ya es el 2");

            // A mitad de la espera sigue parado.
            Avanzar(1.5f);
            Assert.AreEqual(0, _plc.NivelActivo);
            Assert.IsFalse(_plc.SalidaS1);

            // Y al vencer el retardo arranca ya en la velocidad nueva.
            Avanzar(1.7f);
            Assert.AreEqual(2, _plc.NivelActivo);
            Assert.IsTrue(_plc.SalidaS1);
            Assert.IsTrue(_plc.SalidaV(2));
        }

        /// <summary>
        /// Parar, en cambio, es inmediato: el retardo solo retiene el arranque.
        /// Tambien sale del Ladder, donde el TON solo gobierna la subida de VELOCIDAD_n.
        /// </summary>
        [Test]
        public void BajarHastaCero_ParaSinEsperarElRetardo()
        {
            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo);

            _plc.PulsarBajar();

            Assert.AreEqual(0, _plc.NivelActivo, "parar no deberia esperar los 3 s");
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsFalse(_plc.CambioEnCurso, "y no deberia quedar ningun cambio pendiente");
        }

        [Test]
        public void PulsarDosVecesDuranteLaEspera_ReiniciaElTemporizadorYSaltaDosNiveles()
        {
            _plc.PulsarStart();
            SubirYEsperar(); // nivel 1

            _plc.PulsarSubir();
            Avanzar(1f);
            _plc.PulsarSubir(); // segunda pulsacion dentro de la espera

            Assert.AreEqual(3, _plc.NivelPendiente, "las dos pulsaciones se acumulan en el nivel pedido");
            Assert.AreEqual(3f, _plc.TiempoRestanteCambio, 0.1f,
                "el temporizador se reinicia: al cambiar la cuenta cambia que comparacion "
                + "se cumple, y el TON del nivel nuevo empieza de cero");
            Assert.AreEqual(0, _plc.NivelActivo,
                "y mientras tanto el motor esta parado, como en el Ladder original");

            Avanzar(3.2f);
            Assert.AreEqual(3, _plc.NivelActivo);
        }

        // =================================================================================
        // Salidas S1 / S5 / S6 y tabla de velocidades
        // =================================================================================

        [Test]
        public void SalidasS5S6_SiguenLaTablaDelManual()
        {
            _plc.PulsarStart();

            SubirYEsperar(); // nivel 1 -> ninguna entrada
            Assert.IsTrue(_plc.SalidaS1);
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsFalse(_plc.SalidaS6);

            SubirYEsperar(); // nivel 2 -> solo S5
            Assert.IsTrue(_plc.SalidaS5);
            Assert.IsFalse(_plc.SalidaS6);

            SubirYEsperar(); // nivel 3 -> solo S6
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsTrue(_plc.SalidaS6);

            SubirYEsperar(); // nivel 4 -> S5 y S6
            Assert.IsTrue(_plc.SalidaS5);
            Assert.IsTrue(_plc.SalidaS6);
        }

        [Test]
        public void IndicadoresV1aV4_SoloUnoEncendidoALaVez()
        {
            _plc.PulsarStart();

            for (int nivel = 1; nivel <= 4; nivel++)
            {
                SubirYEsperar();
                for (int v = 1; v <= 4; v++)
                {
                    Assert.AreEqual(v == nivel, _plc.SalidaV(v),
                        "en el nivel " + nivel + ", V" + v + " deberia estar " + (v == nivel ? "encendido" : "apagado"));
                }
            }
        }

        // =================================================================================
        // STOP y parada de emergencia
        // =================================================================================

        [Test]
        public void Stop_ParaElMotorYReiniciaLaSeleccion()
        {
            _plc.PulsarStart();
            for (int i = 0; i < 3; i++) SubirYEsperar();
            Assert.AreEqual(3, _plc.NivelActivo);

            _plc.PulsarStop();

            Assert.AreEqual(0, _plc.NivelActivo);
            Assert.IsFalse(_plc.Habilitado);
            Assert.IsFalse(_plc.SalidaS1);

            // Tras STOP hay que volver a pulsar START y se empieza por la velocidad 1.
            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo, "se vuelve a empezar desde la velocidad 1");
        }

        [Test]
        public void Emergencia_CortaTodoDeInmediatoSinEsperarRetardo()
        {
            _plc.PulsarStart();
            for (int i = 0; i < 4; i++) SubirYEsperar();
            Assert.AreEqual(4, _plc.NivelActivo);

            _plc.PulsarEmergencia();

            Assert.IsTrue(_plc.EmergenciaActiva);
            Assert.AreEqual(0, _plc.NivelActivo, "sin retardo: corta en el acto");
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsFalse(_plc.SalidaS6);
            for (int v = 1; v <= 4; v++) Assert.IsFalse(_plc.SalidaV(v));
        }

        [Test]
        public void Emergencia_QuedaEnclavadaHastaRearmar()
        {
            _plc.PulsarEmergencia();

            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(0, _plc.NivelActivo, "con el hongo pulsado no se puede arrancar");
            Assert.IsFalse(_plc.Habilitado);

            _plc.RearmarEmergencia();
            Assert.IsFalse(_plc.EmergenciaActiva);

            _plc.PulsarStart();
            SubirYEsperar();
            Assert.AreEqual(1, _plc.NivelActivo, "tras rearmar vuelve a operar normalmente");
        }

        // =================================================================================
        // Integración PLC -> variador
        // =================================================================================

        [Test]
        public void AplicarA_MueveLosTerminalesDelVariador()
        {
            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            vfd.AplicarConfiguracionDeReferencia();

            _plc.PulsarStart();
            for (int i = 0; i < 4; i++) SubirYEsperar();
            _plc.AplicarA(vfd);

            Assert.IsTrue(vfd.GetTerminal(1));
            Assert.IsTrue(vfd.GetTerminal(5));
            Assert.IsTrue(vfd.GetTerminal(6));
            Assert.AreEqual(50.0f, vfd.ResolverReferenciaHz(), 0.01f, "nivel 4 -> d1-04 = 50 Hz");
        }

        [Test]
        public void AplicarA_LaEmergenciaDelPlcDisparaLaFallaDelVariador()
        {
            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            vfd.AplicarConfiguracionDeReferencia();

            _plc.PulsarStart();
            SubirYEsperar();
            _plc.AplicarA(vfd);

            _plc.PulsarEmergencia();
            _plc.AplicarA(vfd);

            Assert.AreEqual(VfdFault.ParadaEmergencia, vfd.Falla);
            Assert.AreEqual(VfdState.Falla, vfd.Estado);
            Assert.AreEqual(0f, vfd.FrecuenciaSalidaHz, 0.001f);
        }

        [Test]
        public void CadaNivelDelPlcProduceLaFrecuenciaEsperadaEnElVariador()
        {
            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            vfd.AplicarConfiguracionDeReferencia();

            float[] esperadas = { 10.0f, 25.0f, 35.0f, 50.0f };

            _plc.PulsarStart();
            for (int nivel = 1; nivel <= 4; nivel++)
            {
                SubirYEsperar();
                _plc.AplicarA(vfd);

                Assert.AreEqual(esperadas[nivel - 1], vfd.ResolverReferenciaHz(), 0.01f,
                    "nivel " + nivel + " del PLC deberia pedir " + esperadas[nivel - 1] + " Hz");
            }
        }
    }
}
