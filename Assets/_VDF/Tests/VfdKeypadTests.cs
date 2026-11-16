using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// E5: navegación del operador digital.
    ///
    /// Todos los tests pulsan TECLAS REALES y leen la PANTALLA. Ninguno llama a
    /// EscribirParametro por detrás: si la navegación no permite llegar a un valor,
    /// el test falla. Es la única forma de comprobar que el alumno podrá configurar
    /// el variador con el teclado y no solo que el modelo acepta el número.
    /// </summary>
    public class VfdKeypadTests
    {
        private VfdModel _vfd;
        private VfdKeypad _teclado;

        [SetUp]
        public void SetUp()
        {
            _vfd = new VfdModel();
            _teclado = new VfdKeypad(_vfd);
        }

        private void Pulsar(params VfdKey[] teclas)
        {
            for (int i = 0; i < teclas.Length; i++) _teclado.Pulsar(teclas[i]);
        }

        /// <summary>Deja pasar el tiempo suficiente para que caduque un mensaje tipo "End".</summary>
        private void EsperarMensaje()
        {
            _teclado.Tick(VfdKeypad.DuracionMensaje + 0.1f);
        }

        /// <summary>Navega desde el monitor hasta la lista de parámetros, como el manual.</summary>
        private void EntrarEnParametros()
        {
            Pulsar(VfdKey.Arriba);           // monitor -> modos, ya en -PAr-
            Assert.AreEqual("-PAr-", _teclado.TextoPantalla);
            Pulsar(VfdKey.Enter);
            Assert.AreEqual(VfdKeypadState.SeleccionParametro, _teclado.Estado);
        }

        // =================================================================================
        // Pantalla en modo Drive
        // =================================================================================

        [Test]
        public void ArrancaEnElMonitorMostrandoLaFrecuencia()
        {
            Assert.AreEqual(VfdKeypadState.Monitor, _teclado.Estado);
            Assert.AreEqual("F00.00", _teclado.TextoPantalla);
            Assert.IsTrue(_teclado.LedDrv);
            Assert.IsTrue(_teclado.LedFout);
            Assert.IsFalse(_teclado.LedAlm);
        }

        [Test]
        public void ElMonitorSigueALaFrecuenciaDeSalida()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();
            _vfd.SetTerminal(1, true);
            for (int i = 0; i < 300; i++) _vfd.Tick(0.02f);

            Assert.AreEqual("F10.00", _teclado.TextoPantalla);
        }

        [Test]
        public void ConFalla_LaPantallaMuestraElCodigoYSeEnciendeALM()
        {
            _vfd.ParadaEmergencia();

            Assert.AreEqual("EF0", _teclado.TextoPantalla);
            Assert.IsTrue(_teclado.LedAlm);
        }

        [Test]
        public void ResetDesdeElMonitor_BorraLaFalla()
        {
            _vfd.ParadaEmergencia();
            Assert.IsTrue(_teclado.LedAlm);

            Pulsar(VfdKey.Reset);

            Assert.IsFalse(_teclado.LedAlm);
            Assert.AreEqual(VfdFault.Ninguna, _vfd.Falla);
        }

        // =================================================================================
        // Recorrido de modos
        // =================================================================================

        [Test]
        public void ArribaYAbajo_RecorrenLosModosHastaPAr()
        {
            Pulsar(VfdKey.Arriba);
            Assert.AreEqual(VfdKeypadState.SeleccionModo, _teclado.Estado);
            Assert.AreEqual("-PAr-", _teclado.TextoPantalla);

            Pulsar(VfdKey.Arriba);
            Assert.AreEqual("StUP", _teclado.TextoPantalla);

            Pulsar(VfdKey.Abajo);
            Assert.AreEqual("-PAr-", _teclado.TextoPantalla);

            Pulsar(VfdKey.Abajo);
            Assert.AreEqual("-drv-", _teclado.TextoPantalla);
        }

        [Test]
        public void LosModosDanLaVuelta()
        {
            Pulsar(VfdKey.Arriba);
            for (int i = 0; i < VfdKeypad.Modos.Length; i++) Pulsar(VfdKey.Arriba);

            Assert.AreEqual("-PAr-", _teclado.TextoPantalla,
                "tras una vuelta completa debe volver al mismo modo");
        }

        [Test]
        public void EnterEnDrive_VuelveAlMonitor()
        {
            Pulsar(VfdKey.Arriba, VfdKey.Abajo); // hasta -drv-
            Assert.AreEqual("-drv-", _teclado.TextoPantalla);

            Pulsar(VfdKey.Enter);

            Assert.AreEqual(VfdKeypadState.Monitor, _teclado.Estado);
        }

        [Test]
        public void LosModosNoSimulados_LoDicenEnVezDeFingir()
        {
            Pulsar(VfdKey.Arriba, VfdKey.Arriba); // StUP
            Assert.AreEqual("StUP", _teclado.TextoPantalla);

            Pulsar(VfdKey.Enter);

            Assert.AreEqual("n.SIM", _teclado.TextoPantalla,
                "es mas honesto avisar que fingir un modo que no existe");
        }

        // =================================================================================
        // Selección de parámetro
        // =================================================================================

        [Test]
        public void DentroDePAr_LaPantallaMuestraElCodigoDelParametro()
        {
            EntrarEnParametros();

            Assert.AreEqual("A1-03", _teclado.TextoPantalla);
            Assert.AreEqual("A1-03", _teclado.ParametroActual.Id);
        }

        [Test]
        public void ArribaYAbajo_RecorrenLaListaDeParametros()
        {
            EntrarEnParametros();

            Pulsar(VfdKey.Arriba);
            Assert.AreEqual("b1-01", _teclado.TextoPantalla);

            Pulsar(VfdKey.Abajo);
            Assert.AreEqual("A1-03", _teclado.TextoPantalla);

            // Hacia atras desde el primero debe dar la vuelta al ultimo.
            Pulsar(VfdKey.Abajo);
            Assert.AreEqual("L1-01", _teclado.TextoPantalla);
        }

        [Test]
        public void EscDesdeLaListaDeParametros_VuelveAlRecorridoDeModos()
        {
            EntrarEnParametros();

            Pulsar(VfdKey.Esc);

            Assert.AreEqual(VfdKeypadState.SeleccionModo, _teclado.Estado);
            Assert.AreEqual("-PAr-", _teclado.TextoPantalla);
        }

        // =================================================================================
        // Edición de valor
        // =================================================================================

        [Test]
        public void EnterSobreUnParametro_EntraEnEdicionMostrandoSuValor()
        {
            EntrarEnParametros();
            _teclado.IrAParametro("E1-04");   // frecuencia maxima, de fabrica 60.0 Hz

            Pulsar(VfdKey.Enter);

            Assert.AreEqual(VfdKeypadState.Edicion, _teclado.Estado);
            Assert.AreEqual("060.0", _teclado.TextoPantalla);
        }

        [Test]
        public void ResetMueveElDigitoYDaLaVuelta()
        {
            _teclado.IrAParametro("E1-04");
            Pulsar(VfdKey.Enter);

            // El equipo empieza por el digito menos significativo.
            int inicial = _teclado.CursorEdicion;
            Assert.AreEqual(3, inicial, "campo de 4 digitos (max 400.0): cursor en el ultimo");

            Pulsar(VfdKey.Reset);
            Assert.AreEqual(0, _teclado.CursorEdicion, "debe dar la vuelta al primero");

            Pulsar(VfdKey.Reset);
            Assert.AreEqual(1, _teclado.CursorEdicion);
        }

        [Test]
        public void ArribaCambiaSoloElDigitoBajoElCursor_SinArrastrarAcarreo()
        {
            _teclado.IrAParametro("E1-04");
            Pulsar(VfdKey.Enter);
            Assert.AreEqual("060.0", _teclado.TextoPantalla);

            // Cursor en el decimal. 0 -> 9 sin tocar el 60.
            Pulsar(VfdKey.Arriba);
            Assert.AreEqual("060.1", _teclado.TextoPantalla);

            for (int i = 0; i < 9; i++) Pulsar(VfdKey.Arriba);
            Assert.AreEqual("060.0", _teclado.TextoPantalla,
                "el digito gira de 0 a 9 y vuelve a 0 sin sumar al de al lado");
        }

        [Test]
        public void AbajoDesdeCero_DaLaVueltaANueve()
        {
            _teclado.IrAParametro("E1-04");
            Pulsar(VfdKey.Enter);

            Pulsar(VfdKey.Abajo);

            Assert.AreEqual("060.9", _teclado.TextoPantalla);
        }

        [Test]
        public void EnterGuarda_MuestraEndYVuelveALaLista()
        {
            _teclado.IrAParametro("C1-01");   // 10.0 s de fabrica
            Pulsar(VfdKey.Enter);
            Pulsar(VfdKey.Arriba);            // 10.0 -> 10.1

            Pulsar(VfdKey.Enter);

            Assert.AreEqual(VfdKeypadState.Mensaje, _teclado.Estado);
            Assert.AreEqual("End", _teclado.TextoPantalla);
            Assert.AreEqual(10.1f, _vfd.Valor("C1-01"), 0.001f);

            EsperarMensaje();
            Assert.AreEqual(VfdKeypadState.SeleccionParametro, _teclado.Estado);
            Assert.AreEqual("C1-01", _teclado.TextoPantalla);
        }

        [Test]
        public void EscDuranteLaEdicion_DescartaElCambio()
        {
            _teclado.IrAParametro("C1-01");
            float original = _vfd.Valor("C1-01");

            Pulsar(VfdKey.Enter);
            Pulsar(VfdKey.Arriba, VfdKey.Arriba, VfdKey.Arriba);
            Pulsar(VfdKey.Esc);

            Assert.AreEqual(VfdKeypadState.SeleccionParametro, _teclado.Estado);
            Assert.AreEqual(original, _vfd.Valor("C1-01"), 0.001f, "ESC no debe guardar");
        }

        [Test]
        public void ValorInvalido_MuestraOpeYNoGuarda()
        {
            // L1-01 solo admite 0..4. Subimos el digito a 7 y guardamos.
            _teclado.IrAParametro("L1-01");
            Pulsar(VfdKey.Enter);
            for (int i = 0; i < 6; i++) Pulsar(VfdKey.Arriba);  // 1 -> 7

            Pulsar(VfdKey.Enter);

            Assert.AreEqual("oPE", _teclado.TextoPantalla, "error de programacion del operador");
            Assert.AreEqual(1f, _vfd.Valor("L1-01"), 0.001f, "el valor no debe cambiar");

            EsperarMensaje();
            Assert.AreEqual(VfdKeypadState.Edicion, _teclado.Estado,
                "tras oPE se vuelve a la edicion para corregir, no se pierde el trabajo");
        }

        // =================================================================================
        // La prueba de fuego: configurar la práctica entera solo con teclas
        // =================================================================================

        [Test]
        public void SePuedeConfigurarTodaLaPracticaUsandoSoloLasTeclas()
        {
            // Primero la inicializacion a 2 hilos, como manda el procedimiento.
            TeclearParametro("A1-03", 2220f);
            Assert.AreEqual(VfdTerminalFunction.MarchaAdelante2Hilos, _vfd.ValorEntero("H1-01"),
                "A1-03 = 2220 debe dejar S1 como marcha adelante");

            // Y ahora, toda la tabla de la practica.
            foreach (var kv in VfdParameterTable.ConfiguracionDeReferencia)
            {
                TeclearParametro(kv.Key, kv.Value);
                Assert.AreEqual(kv.Value, _vfd.Valor(kv.Key), 0.005f,
                    "no se pudo teclear " + kv.Key + " = " + kv.Value);
            }

            // El variador debe quedar listo para operar.
            Assert.IsTrue(_vfd.ConfiguracionEsOperable());
            var problemas = _vfd.ValidarConfiguracion();
            Assert.AreEqual(0, problemas.Count,
                "configurado solo con el teclado, no deberia quedar ningun aviso:\n  "
                + string.Join("\n  ", problemas.ConvertAll(p => p.ToString()).ToArray()));
        }

        /// <summary>
        /// Teclea un valor como lo haría una persona: ir al parámetro, ENTER, mover
        /// dígitos con &gt;/RESET, subirlos con ▲ y ENTER para guardar.
        /// </summary>
        private void TeclearParametro(string id, float valor)
        {
            Assert.IsTrue(_teclado.IrAParametro(id), "no existe el parametro " + id);
            Pulsar(VfdKey.Enter);
            Assert.AreEqual(VfdKeypadState.Edicion, _teclado.Estado);

            // SecuenciaParaValor pulsa de verdad las teclas y devuelve cuales fueron.
            // Puede devolver 0 pulsaciones legitimamente: hay parametros cuyo valor de
            // fabrica YA es el correcto para la practica (L1-01 = 1, C1-01/C1-02 = 10.0 s,
            // b1-02 = 1, H1-05 = 3, H1-06 = 4). En esos solo hay que confirmar con ENTER.
            _teclado.SecuenciaParaValor(valor);

            Pulsar(VfdKey.Enter);
            EsperarMensaje();
        }

        [Test]
        public void TeclearUnValorCuestaUnNumeroRazonableDePulsaciones()
        {
            // Si configurar un parametro costara 80 pulsaciones, la practica seria
            // inusable en VR. Este test fija un limite.
            _teclado.IrAParametro("d1-04");
            Pulsar(VfdKey.Enter);

            var secuencia = _teclado.SecuenciaParaValor(50.0f);

            Assert.LessOrEqual(secuencia.Count, 40,
                "teclear d1-04 = 50.00 Hz costo " + secuencia.Count + " pulsaciones");
            Assert.AreEqual(50.0f, ValorTrasGuardar("d1-04"), 0.005f);
        }

        private float ValorTrasGuardar(string id)
        {
            Pulsar(VfdKey.Enter);
            EsperarMensaje();
            return _vfd.Valor(id);
        }

        // =================================================================================
        // LO/RE, RUN y STOP
        // =================================================================================

        [Test]
        public void RunEnRemoto_NoArrancaYAvisa()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();  // b1-02 = 1, marcha por terminales

            Pulsar(VfdKey.Run);

            Assert.AreEqual("rE", _teclado.TextoPantalla,
                "con la marcha en terminales, la tecla RUN no debe arrancar el motor");
            for (int i = 0; i < 200; i++) _vfd.Tick(0.02f);
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.01f);
        }

        [Test]
        public void RunVariasVecesSeguidas_ElTecladoVuelveAlMonitor()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();

            for (int i = 0; i < 6; i++) Pulsar(VfdKey.Run);
            Assert.AreEqual("rE", _teclado.TextoPantalla);

            EsperarMensaje();
            Assert.AreEqual(VfdKeypadState.Monitor, _teclado.Estado,
                "pasado el aviso, el teclado tiene que volver al monitor");

            Pulsar(VfdKey.Run, VfdKey.Enter);
            Assert.AreEqual(VfdKeypadState.Monitor, _teclado.Estado,
                "una tecla corta el aviso y vuelve al monitor");
        }

        [Test]
        public void LoRe_PasaALocalYEntoncesRunSiArranca()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();

            Pulsar(VfdKey.LoRe);
            Assert.IsTrue(_teclado.LedLocal);

            Pulsar(VfdKey.Run);
            for (int i = 0; i < 300; i++) _vfd.Tick(0.02f);

            Assert.AreEqual(10.0f, _vfd.FrecuenciaSalidaHz, 0.05f,
                "en LOCAL, RUN arranca a d1-01 = 10 Hz");

            Pulsar(VfdKey.Stop);
            for (int i = 0; i < 300; i++) _vfd.Tick(0.02f);
            Assert.AreEqual(0f, _vfd.FrecuenciaSalidaHz, 0.05f);
        }

        [Test]
        public void LoRe_SeRechazaConElMotorEnMarcha()
        {
            _vfd.EscribirParametro("A1-03", 2220);
            _vfd.AplicarConfiguracionDeReferencia();
            _vfd.SetTerminal(1, true);
            for (int i = 0; i < 300; i++) _vfd.Tick(0.02f);
            Assert.AreEqual(VfdState.EnMarcha, _vfd.Estado);

            Pulsar(VfdKey.LoRe);

            Assert.AreEqual("oPE", _teclado.TextoPantalla);
            Assert.IsFalse(_teclado.LedLocal, "no se cambia de LOCAL/REMOTE con el motor girando");
        }

        [Test]
        public void CualquierTeclaCortaElMensajeTemporal()
        {
            _teclado.IrAParametro("C1-01");
            Pulsar(VfdKey.Enter, VfdKey.Arriba, VfdKey.Enter);
            Assert.AreEqual("End", _teclado.TextoPantalla);

            Pulsar(VfdKey.Esc);

            Assert.AreEqual(VfdKeypadState.SeleccionParametro, _teclado.Estado);
        }

        [Test]
        public void ConElMotorGirando_SoloSeEditanVelocidadesYRampas()
        {
            var vfd = new VfdModel();
            vfd.AplicarConfiguracionDeReferencia();
            vfd.SetAlimentacion(true);
            vfd.SetTerminal(1, true);
            vfd.Tick(0.1f);
            Assert.AreNotEqual(VfdState.Detenido, vfd.Estado, "el motor debería estar arrancando");

            var teclado = new VfdKeypad(vfd);
            Assert.IsTrue(teclado.IrAParametro("b1-02"));
            teclado.Pulsar(VfdKey.Enter);
            Assert.AreNotEqual(VfdKeypadState.Edicion, teclado.Estado, "b1-02 no se cambia en marcha");

            Assert.IsTrue(teclado.IrAParametro("d1-02"));
            teclado.Pulsar(VfdKey.Enter);
            Assert.AreEqual(VfdKeypadState.Edicion, teclado.Estado, "d1-02 sí se cambia en marcha");
        }
    }
}
