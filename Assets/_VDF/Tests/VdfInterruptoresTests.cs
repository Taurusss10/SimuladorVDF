using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    public class VdfInterruptoresTests
    {
        private PlcFanLogic _plc;

        [SetUp]
        public void SetUp()
        {
            VdfMando.Activo = VdfModoMando.Interruptores;
            _plc = new PlcFanLogic();
            _plc.CambiarModo(VdfModoMando.Interruptores);
        }

        [TearDown]
        public void TearDown()
        {
            // El modo activo es estático: si se quedara en interruptores, el resto de tests
            // validaría contra el cableado y la configuración del laboratorio.
            VdfMando.Activo = VdfModoMando.Pulsadores;
        }

        private void Avanzar(float segundos)
        {
            float t = 0f;
            while (t < segundos)
            {
                float dt = Mathf.Min(0.05f, segundos - t);
                _plc.Tick(dt);
                t += dt;
            }
        }

        private void Esperar() { Avanzar(PlcFanLogic.RetardoCambioNivelSegundos + 0.2f); }

        /// <summary>Variador inicializado y con la configuración del laboratorio.</summary>
        private static VfdModel VariadorDelLaboratorio()
        {
            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            vfd.AplicarConfiguracionDeReferencia();
            return vfd;
        }

        /// <summary>Avanza el PLC y el variador juntos, como hace la simulación cada fotograma.</summary>
        private void Correr(VfdModel vfd, float segundos)
        {
            float t = 0f;
            while (t < segundos)
            {
                float dt = Mathf.Min(0.05f, segundos - t);
                _plc.Tick(dt);
                _plc.AplicarA(vfd);
                vfd.Tick(dt);
                t += dt;
            }
        }

        // =================================================================================
        // Lógica: el Ladder del laboratorio
        // =================================================================================

        [Test]
        public void PorDefecto_ElPlcVaConPulsadores()
        {
            Assert.AreEqual(VdfModoMando.Pulsadores, new PlcFanLogic().Modo);
        }

        [Test]
        public void LaTablaDeVelocidades_EsLaDelVariador()
        {
            Assert.AreEqual(1, PlcFanLogic.NivelDeInterruptores(false, false));
            Assert.AreEqual(2, PlcFanLogic.NivelDeInterruptores(true, false));
            Assert.AreEqual(3, PlcFanLogic.NivelDeInterruptores(false, true));
            Assert.AreEqual(4, PlcFanLogic.NivelDeInterruptores(true, true));
        }

        [Test]
        public void MarchaArriba_DaLaMarchaEnElActo_YElPilotoV1A3s()
        {
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);

            Assert.IsTrue(_plc.Habilitado);
            Assert.IsTrue(_plc.SalidaS1, "Q0.5 copia I0.0 sin retardo");
            Assert.IsFalse(_plc.SalidaV(1), "el piloto espera a su TON de 3 s");

            Esperar();
            Assert.AreEqual(1, _plc.NivelActivo);
            Assert.IsTrue(_plc.SalidaV(1));
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsFalse(_plc.SalidaS6);
        }

        [Test]
        public void LaMarchaLlegaPorS4_YS1NoRecibeNada()
        {
            Assert.AreEqual(4, _plc.TerminalMarcha);
            var vfd = VariadorDelLaboratorio();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.AplicarA(vfd);

            Assert.IsTrue(vfd.GetTerminal(4));
            Assert.IsFalse(vfd.GetTerminal(1));
        }

        [Test]
        public void CadaCombinacion_DaSuFrecuenciaEnElVariador()
        {
            var vfd = VariadorDelLaboratorio();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);

            bool[,] combinaciones = { { false, false }, { true, false }, { false, true }, { true, true } };
            float[] esperadas = { 10f, 25f, 35f, 50f };

            for (int i = 0; i < 4; i++)
            {
                _plc.FijarInterruptor(VdfInterruptor.S5, combinaciones[i, 0]);
                _plc.FijarInterruptor(VdfInterruptor.S6, combinaciones[i, 1]);
                Correr(vfd, 12f);

                Assert.AreEqual(i + 1, _plc.NivelActivo);
                Assert.IsTrue(_plc.SalidaV(i + 1), "piloto V" + (i + 1));
                Assert.AreEqual(esperadas[i], vfd.FrecuenciaSalidaHz, 0.01f,
                    "S5=" + combinaciones[i, 0] + " S6=" + combinaciones[i, 1]);
            }
        }

        [Test]
        public void CambiarDeVelocidad_NoParaElMotor_SoloRetrasaElPiloto()
        {
            var vfd = VariadorDelLaboratorio();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            Correr(vfd, 12f);
            Assert.AreEqual(10f, vfd.FrecuenciaSalidaHz, 0.01f);

            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            Assert.IsTrue(_plc.SalidaS1, "la marcha sigue");
            Assert.IsTrue(_plc.SalidaS5, "Q0.6 copia I0.1 sin retardo");
            Assert.IsFalse(_plc.SalidaV(1), "el piloto viejo se apaga en el acto");
            Assert.IsFalse(_plc.SalidaV(2), "el nuevo espera 3 s");

            float minima = float.MaxValue;
            for (int i = 0; i < 40; i++)
            {
                Correr(vfd, 0.05f);
                minima = Mathf.Min(minima, vfd.FrecuenciaSalidaHz);
            }
            Assert.GreaterOrEqual(minima, 10f - 0.01f, "el motor no se para durante el cambio");
            Assert.Greater(vfd.FrecuenciaSalidaHz, 10f, "ya está subiendo hacia 25 Hz");

            Correr(vfd, 2f);
            Assert.IsTrue(_plc.SalidaV(2));
        }

        [Test]
        public void SinRetardo_ElPilotoTambienEsInmediato()
        {
            _plc.RetardoConInterruptores = false;
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.FijarInterruptor(VdfInterruptor.S6, true);
            Assert.AreEqual(3, _plc.NivelActivo);
            Assert.IsTrue(_plc.SalidaV(3));
        }

        [Test]
        public void MarchaAbajo_QuitaLaMarchaYApagaLosPilotos()
        {
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            Esperar();

            _plc.FijarInterruptor(VdfInterruptor.Marcha, false);

            Assert.IsFalse(_plc.Habilitado);
            Assert.AreEqual(0, _plc.NivelActivo);
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsFalse(_plc.SalidaV(2));
        }

        [Test]
        public void S5yS6SinMarcha_CopianSusSalidas_PeroNiArrancanNiEncienden()
        {
            var vfd = VariadorDelLaboratorio();
            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            _plc.FijarInterruptor(VdfInterruptor.S6, true);
            Correr(vfd, 5f);

            Assert.IsFalse(_plc.Habilitado);
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsTrue(_plc.SalidaS5, "I0.1 → Q0.6 no depende de la marcha, como en el Ladder");
            Assert.IsFalse(_plc.SalidaV(4), "los pilotos sí cuelgan de S1");
            Assert.AreEqual(0f, vfd.FrecuenciaSalidaHz, 0.001f);
        }

        [Test]
        public void ConInterruptores_LosPulsadoresNoHacenNada()
        {
            _plc.PulsarStart();
            _plc.PulsarSubir();
            Esperar();
            Assert.IsFalse(_plc.Habilitado);
            Assert.AreEqual(0, _plc.NivelActivo);

            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            Esperar();
            _plc.PulsarStop();
            Assert.IsTrue(_plc.Habilitado, "STOP del tablero no para: manda el interruptor");
        }

        [Test]
        public void ConPulsadores_LosInterruptoresNoHacenNada_YElMotorSigueParandoEntreVelocidades()
        {
            var plc = new PlcFanLogic();
            plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            plc.FijarInterruptor(VdfInterruptor.S5, true);
            Assert.IsFalse(plc.Habilitado);
            Assert.AreEqual(1, plc.TerminalMarcha);

            // Y los pulsadores siguen como siempre, con el retardo que para el motor.
            plc.PulsarStart();
            plc.PulsarSubir();
            Assert.IsFalse(plc.SalidaS1, "con pulsadores la marcha espera los 3 s");
            plc.Tick(PlcFanLogic.RetardoCambioNivelSegundos + 0.1f);
            Assert.AreEqual(1, plc.NivelActivo);
            Assert.IsTrue(plc.SalidaS1);
        }

        [Test]
        public void LaEmergencia_CortaAunqueLaMarchaSigaArriba()
        {
            var vfd = VariadorDelLaboratorio();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            Correr(vfd, 5f);

            _plc.PulsarEmergencia();
            _plc.AplicarA(vfd);

            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsFalse(_plc.SalidaS5);
            Assert.IsFalse(vfd.GetTerminal(4));
            Assert.AreEqual(VfdFault.ParadaEmergencia, vfd.Falla);
        }

        [Test]
        public void TrasRearmar_ElMotorNoArrancaSolo_HayQueBajarYSubirLaMarcha()
        {
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            Esperar();
            _plc.PulsarEmergencia();
            _plc.RearmarEmergencia();

            // Tocar S5 con la marcha todavía arriba no puede arrancarlo.
            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            Esperar();
            Assert.IsFalse(_plc.Habilitado);
            Assert.IsFalse(_plc.SalidaS1);
            Assert.IsTrue(_plc.MarchaBloqueadaTrasEmergencia);

            _plc.FijarInterruptor(VdfInterruptor.Marcha, false);
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            Esperar();
            Assert.AreEqual(2, _plc.NivelActivo);
        }

        [Test]
        public void SubirLaMarchaConLaEmergenciaPuesta_TampocoArrancaAlRearmar()
        {
            _plc.PulsarEmergencia();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.RearmarEmergencia();
            _plc.FijarInterruptor(VdfInterruptor.S6, true);
            Esperar();
            Assert.IsFalse(_plc.Habilitado);
            Assert.IsFalse(_plc.SalidaS1);
        }

        [Test]
        public void ConH104DeFabrica_LaMarchaNoArrancaElMotor()
        {
            var vfd = VariadorDelLaboratorio();
            vfd.EscribirParametro("H1-04", 14);
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            Correr(vfd, 5f);

            Assert.IsTrue(_plc.SalidaS1, "el PLC sí manda");
            Assert.AreEqual(0f, vfd.FrecuenciaSalidaHz, 0.001f, "pero S4 es reset de falla, no marcha");
        }

        [Test]
        public void Reiniciar_BajaLosTresInterruptores()
        {
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true);
            _plc.FijarInterruptor(VdfInterruptor.S5, true);
            _plc.FijarInterruptor(VdfInterruptor.S6, true);
            _plc.Reiniciar();

            Assert.IsFalse(_plc.InterruptorMarcha);
            Assert.IsFalse(_plc.InterruptorS5);
            Assert.IsFalse(_plc.InterruptorS6);
            Assert.AreEqual(VdfModoMando.Interruptores, _plc.Modo, "reiniciar no cambia de modo");
        }

        // =================================================================================
        // Configuración del variador del laboratorio
        // =================================================================================

        [Test]
        public void LaConfiguracionDelLaboratorio_CambiaSoloE201H101yH104()
        {
            var lab = VfdParameterTable.ConfiguracionLaboratorio;
            var pul = VfdParameterTable.ConfiguracionDeReferencia;
            Assert.AreEqual(1.9f, lab["E2-01"], 0.001f);
            Assert.AreEqual(14f, lab["H1-01"]);
            Assert.AreEqual(40f, lab["H1-04"]);
            Assert.AreEqual(1.2f, pul["E2-01"], 0.001f, "la de pulsadores no se toca");
            Assert.AreEqual(40f, pul["H1-01"]);
            Assert.IsFalse(pul.ContainsKey("H1-04"));

            foreach (var kv in pul)
                if (kv.Key != "E2-01" && kv.Key != "H1-01")
                    Assert.AreEqual(kv.Value, lab[kv.Key], 0.001f, kv.Key);
        }

        [Test]
        public void ConLaConfiguracionDelLaboratorio_NoHayAvisos()
        {
            CollectionAssert.IsEmpty(VariadorDelLaboratorio().ValidarConfiguracion());
        }

        [Test]
        public void ConH104DeFabrica_ElAvisoEsCriticoYApuntaAH104()
        {
            var vfd = VariadorDelLaboratorio();
            vfd.EscribirParametro("H1-04", 14);
            var problemas = vfd.ValidarConfiguracion();
            Assert.IsTrue(problemas.Exists(p => p.ParametroId == "H1-04" && p.Severidad == VfdIssueSeverity.Critica));
            Assert.IsFalse(vfd.ConfiguracionEsOperable());
        }

        [Test]
        public void ConH101EnCuarenta_AvisaQueDebeSer14()
        {
            var vfd = VariadorDelLaboratorio();
            vfd.EscribirParametro("H1-01", 40);
            var problemas = vfd.ValidarConfiguracion();
            Assert.IsTrue(problemas.Exists(p => p.ParametroId == "H1-01" && p.Severidad == VfdIssueSeverity.Advertencia));
            Assert.IsTrue(vfd.ConfiguracionEsOperable(), "el motor funciona igual: S1 no recibe nada");
        }

        [Test]
        public void SeguirElGuionDeLaPractica2DelLaboratorio_DejaElVariadorSinAvisos()
        {
            var p2 = VdfPracticasDefaults.CrearConfiguracion();
            p2.UsarMando(VdfModoMando.Interruptores);
            CollectionAssert.IsEmpty(p2.Validar());

            var vfd = new VfdModel();
            vfd.EscribirParametro("A1-03", 2220);
            foreach (var paso in p2.Pasos)
            {
                if (paso.Condicion == VdfCondicion.ParametroEnValor || paso.Condicion == VdfCondicion.ParametroRevisado)
                    Assert.IsTrue(vfd.EscribirParametro(paso.Objetivo, paso.Valor), paso.Objetivo);
            }

            CollectionAssert.IsEmpty(vfd.ValidarConfiguracion(),
                "el guion tiene que pedir todo lo que la validación exige");
        }

        // =================================================================================
        // Cableado del laboratorio
        // =================================================================================

        [Test]
        public void ElCableadoDelLaboratorio_EsElDelCuaderno()
        {
            var grafo = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(grafo);

            Assert.IsTrue(VdfWiringSpec.Validar(grafo).EsCorrecto);
            Assert.IsTrue(grafo.MismaRed("PLC-Q0.5", "S4"));
            Assert.IsTrue(grafo.MismaRed("PLC-Q0.6", "S5"));
            Assert.IsTrue(grafo.MismaRed("PLC-Q0.7", "S6"));
            Assert.IsTrue(grafo.MismaRed("PLC-2L", "SC"));
            Assert.IsTrue(grafo.MismaRed("BRK-L1", "PLC-1L"));
            Assert.IsFalse(grafo.MismaRed("PLC-Q0.7", "S1"), "S1 se queda sin cable");

            var puerta = VdfPuertaCableado.Calcular(grafo);
            Assert.IsTrue(puerta.Alimentado && puerta.ComunDeEntradas && puerta.S4 && puerta.S5 && puerta.S6 && puerta.Motor);
            for (int v = 1; v <= 4; v++) Assert.IsTrue(puerta.Piloto(v), "V" + v);
        }

        [Test]
        public void ElCableadoDePulsadores_NoValeEnElLaboratorio_YAlReves()
        {
            var conPulsadores = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(conPulsadores, VdfWiringSpec.Practica(VdfModoMando.Pulsadores));
            Assert.IsFalse(VdfWiringSpec.Validar(conPulsadores, VdfWiringSpec.Practica(VdfModoMando.Interruptores)).EsCorrecto);
            Assert.IsFalse(VdfPuertaCableado.Calcular(conPulsadores, VdfModoMando.Interruptores).S4);

            var delLab = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(delLab, VdfWiringSpec.Practica(VdfModoMando.Interruptores));
            Assert.IsFalse(VdfWiringSpec.Validar(delLab, VdfWiringSpec.Practica(VdfModoMando.Pulsadores)).EsCorrecto);
        }

        [Test]
        public void SinElCable2LaSC_NingunaEntradaLlega()
        {
            var grafo = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(grafo);
            grafo.Desconectar("PLC-2L", "SC");

            var puerta = VdfPuertaCableado.Calcular(grafo);
            Assert.IsTrue(puerta.Alimentado);
            Assert.IsFalse(puerta.S4 || puerta.S5 || puerta.S6);
        }

        [Test]
        public void LosPasosDeLaPractica3DelLaboratorio_UnenBornesDeLaMismaRed()
        {
            var p3 = VdfPracticasDefaults.CrearCableado();
            p3.UsarMando(VdfModoMando.Interruptores);
            CollectionAssert.IsEmpty(p3.Validar());

            var grafo = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(grafo);
            foreach (var paso in p3.Pasos)
            {
                if (paso.Condicion != VdfCondicion.BornesUnidos) continue;
                var b = paso.Objetivo.Split('|');
                Assert.IsTrue(grafo.MismaRed(b[0], b[1]), paso.Objetivo + " no es una unión del cableado correcto");
            }
        }

        // =================================================================================
        // Averías del laboratorio
        // =================================================================================

        [Test]
        public void ElCatalogoDelLaboratorio_CambiaLaAveriaDeMarchaYLaDelComun()
        {
            var lab = VdfCatalogoFallas.Crear(VdfModoMando.Interruptores);
            var pul = VdfCatalogoFallas.Crear(VdfModoMando.Pulsadores);
            Assert.AreEqual(pul.Count, lab.Count);
            for (int i = 0; i < lab.Count; i++)
                Assert.AreEqual(pul[i].Titulo, lab[i].Titulo, "los botones del panel muestran los títulos");

            Assert.IsNotNull(lab.Find(f => f.Id == "H1-04_RESET"));
            Assert.IsNull(lab.Find(f => f.Id == "H1-01_LOCAL_REMOTE"));
            Assert.AreEqual("PLC-2L", lab.Find(f => f.Id == "SC_SIN_COMUN").ObjetivoA);
        }

        [Test]
        public void CadaAveriaDelLaboratorio_SeProvocaYSeRepara()
        {
            foreach (var falla in VdfCatalogoFallas.Crear(VdfModoMando.Interruptores))
            {
                var vfd = VariadorDelLaboratorio();
                var grafo = new VdfWiringGraph();
                VdfWiringSpec.AplicarCableadoCorrecto(grafo);

                Assert.IsTrue(falla.Reparada(vfd, grafo), falla.Id + " ya presente en el módulo sano");
                Assert.IsTrue(falla.Aplicar(vfd, grafo), falla.Id + " no se pudo provocar");
                Assert.IsFalse(falla.Reparada(vfd, grafo), falla.Id);
            }
        }

        // =================================================================================
        // Pieza de escena y simulación
        // =================================================================================

        [Test]
        public void ElInterruptorDeEscena_ConmutaYMueveLaPalanca()
        {
            var raiz = new GameObject("RaizInterruptores");
            try
            {
                var sim = raiz.AddComponent<VdfSimulacion>();
                sim.Despertar();
                sim.CambiarModoMando(VdfModoMando.Interruptores);
                Assert.AreEqual(VdfModoMando.Interruptores, VdfMando.Activo, "la simulación fija el modo activo");

                var caja = VdfInterruptoresBuilder.Construir();
                caja.transform.SetParent(raiz.transform, true);

                var marcha = caja.transform.Find(VdfInterruptoresBuilder.NombreMarcha).GetComponent<VdfPulsadorTablero>();
                marcha.Simulacion = sim;
                Assert.IsTrue(marcha.EsInterruptor);

                marcha.Pulsar();
                Assert.IsTrue(sim.Plc.InterruptorMarcha);
                Assert.IsTrue(sim.Plc.Habilitado);
                Assert.IsTrue(marcha.PalancaArriba);

                marcha.Pulsar();
                Assert.IsFalse(sim.Plc.InterruptorMarcha);
                Assert.IsFalse(marcha.PalancaArriba);

                sim.CambiarModoMando(VdfModoMando.Pulsadores);
                Assert.AreEqual(VdfModoMando.Pulsadores, VdfMando.Activo);
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        [Test]
        public void LaCaja_TieneLosTresInterruptoresConSuColliderYSuAccion()
        {
            var caja = VdfInterruptoresBuilder.Construir();
            try
            {
                Assert.AreEqual(VdfSimulacion.NombreInterruptores, caja.name);
                string[] nombres = { VdfInterruptoresBuilder.NombreMarcha, VdfInterruptoresBuilder.NombreS5, VdfInterruptoresBuilder.NombreS6 };
                VdfAccionPulsador[] acciones = { VdfAccionPulsador.InterruptorMarcha, VdfAccionPulsador.InterruptorS5, VdfAccionPulsador.InterruptorS6 };

                for (int i = 0; i < 3; i++)
                {
                    var nodo = caja.transform.Find(nombres[i]);
                    Assert.IsNotNull(nodo, nombres[i]);
                    Assert.AreEqual(acciones[i], nodo.GetComponent<VdfPulsadorTablero>().Accion);
                    Assert.IsNotNull(nodo.GetComponent<Collider>(), "el rayo y el dedo buscan el collider del nodo");
                    Assert.AreEqual(1, nodo.GetComponentsInChildren<Collider>().Length, "un solo collider por interruptor");
                    Assert.IsNotNull(nodo.Find("Palanca"));
                }

                Assert.GreaterOrEqual(caja.GetComponentInChildren<Renderer>().bounds.min.y,
                    VdfReconstructorEscena.AlturaEncimera - 0.005f, "apoyada en la mesa, no hundida");
            }
            finally
            {
                Object.DestroyImmediate(caja);
            }
        }

        // =================================================================================
        // Prácticas y Ladder
        // =================================================================================

        [Test]
        public void LasCincoPracticas_TienenGuionParaLosDosModos_YNoSeBorraNinguno()
        {
            foreach (var p in VdfPracticasDefaults.CrearTodas())
            {
                var conPulsadores = p.Pasos;
                Assert.Greater(p.PasosConInterruptores.Count, 0, "práctica " + p.Numero);

                p.UsarMando(VdfModoMando.Interruptores);
                CollectionAssert.IsEmpty(p.Validar(), "práctica " + p.Numero + " con interruptores");
                p.UsarMando(VdfModoMando.Pulsadores);
                Assert.AreSame(conPulsadores, p.Pasos, "volver a pulsadores recupera el guion original");
                CollectionAssert.IsEmpty(p.Validar(), "práctica " + p.Numero + " con pulsadores");
            }

            var p4 = VdfPracticasDefaults.CrearOperacion();
            p4.UsarMando(VdfModoMando.Interruptores);
            StringAssert.Contains("interruptor S4", p4.Pasos[1].Instruccion);
            p4.UsarMando(VdfModoMando.Pulsadores);
            StringAssert.Contains("START", p4.Pasos[1].Instruccion);

            var p5 = VdfPracticasDefaults.CrearSeguridadYFallas();
            Assert.AreEqual(p5.Pasos.Count, p5.PasosConInterruptores.Count, "la 5 es el mismo guion");
            p5.UsarMando(VdfModoMando.Interruptores);
            Assert.IsTrue(p5.NecesitaAveria());
        }

        [Test]
        public void ElGuionConInterruptores_NoNombraLosPulsadores()
        {
            foreach (var paso in VdfPracticasDefaults.PasosOperacionConInterruptores())
            {
                // Solo la instrucción: la pista puede hablar de lo que hacía START.
                string texto = paso.Instruccion;
                Assert.IsFalse(texto.Contains("START") || texto.Contains("SUBIR") || texto.Contains("BAJAR"),
                    "manda usar un pulsador: " + texto);
            }
        }

        [Test]
        public void LosGuionesDelLaboratorio_NoMandanAS1()
        {
            // La marcha va por S4: un paso que pidiera cablear o revisar S1 como marcha
            // dejaría al alumno con el motor parado.
            var p3 = VdfPracticasDefaults.CrearCableado();
            p3.UsarMando(VdfModoMando.Interruptores);
            foreach (var paso in p3.Pasos)
                Assert.IsFalse(paso.Objetivo != null && paso.Objetivo.Contains("|S1"), paso.Objetivo);

            var p1 = VdfPracticasDefaults.CrearReconocimiento();
            p1.UsarMando(VdfModoMando.Interruptores);
            Assert.IsTrue(p1.Pasos.Exists(p => p.Objetivo == "S4"));
            Assert.IsFalse(p1.Pasos.Exists(p => p.Objetivo == "S1"));
        }

        [Test]
        public void ElLadderDelLaboratorio_CoincideConElPlcEnTodaLaSecuencia()
        {
            var peldanos = VdfLadderPrograma.Crear(VdfModoMando.Interruptores);
            Assert.LessOrEqual(peldanos.Count, VdfLadderPrograma.Crear().Count, "cabe en el tablero del Ladder");
            var estado = new VdfEstadoLadder(_plc);

            System.Action comprobar = () =>
                CollectionAssert.IsEmpty(VdfLadderPrograma.Contrastar(peldanos, estado));

            comprobar();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true); comprobar();
            Avanzar(1f); comprobar();
            Esperar(); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.S5, true); comprobar();
            Esperar(); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.S6, true); Esperar(); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.S5, false); Avanzar(1f); comprobar();
            _plc.PulsarEmergencia(); comprobar();
            _plc.RearmarEmergencia(); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.S5, true); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, false); comprobar();
            _plc.FijarInterruptor(VdfInterruptor.Marcha, true); Esperar(); comprobar();
        }

        [Test]
        public void ElLadderDelLaboratorio_TieneLasSalidasDelPrograma()
        {
            var peldanos = VdfLadderPrograma.Crear(VdfModoMando.Interruptores);
            var bobinas = new System.Collections.Generic.List<string>();
            foreach (var p in peldanos) bobinas.Add(p.Bobina.Etiqueta);

            CollectionAssert.IsSubsetOf(new[] { "V1 Q0.0", "V2 Q0.1", "V3 Q0.2", "V4 Q0.3", "H1-04 Q0.5", "H1-05 Q0.6", "H1-06 Q0.7" }, bobinas);
        }

        [Test]
        public void ElCableadoDelLaboratorio_SaleDeF2YNoDelBorneDelCentro()
        {
            bool usaF2 = false;
            foreach (var red in VdfWiringSpec.Laboratorio())
                foreach (var b in red.Bornes)
                {
                    Assert.AreNotEqual("BRK-L2", b, "el borne del centro del breaker no se usa");
                    if (b == "BRK-L3") usaF2 = true;
                }
            Assert.IsTrue(usaF2, "S/L2 y los pilotos van a F2 (BRK-L3)");

            var g = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(g, VdfWiringSpec.Laboratorio());
            var puerta = VdfPuertaCableado.Calcular(g, VdfModoMando.Interruptores);
            Assert.IsTrue(puerta.Alimentado);
            for (int i = 0; i < 4; i++) Assert.IsTrue(puerta.V[i], "piloto V" + (i + 1));
        }

        [Test]
        public void LaPractica1DelLaboratorio_PideLosInterruptoresYNoLosPulsadores()
        {
            var p1 = VdfPracticasDefaults.CrearReconocimiento();
            bool interruptores = false;
            foreach (var paso in p1.PasosConInterruptores)
            {
                Assert.AreNotEqual("Mando", paso.Objetivo, "START, STOP, SUBIR y BAJAR no se usan en el laboratorio");
                if (paso.Objetivo == VdfPracticasDefaults.GrupoMandoLaboratorio) interruptores = true;
            }
            Assert.IsTrue(interruptores);

            bool pulsadores = false;
            foreach (var paso in p1.Pasos) if (paso.Objetivo == "Mando") pulsadores = true;
            Assert.IsTrue(pulsadores, "el guion de pulsadores sigue pidiendo los pulsadores");
        }

        [Test]
        public void LaEtiquetaDeCadaInterruptor_DiceSuEntradaDelPlc()
        {
            StringAssert.Contains("I0.0", VdfEtiquetaFlotante.NombreDePulsador(VdfInterruptoresBuilder.NombreMarcha));
            StringAssert.StartsWith("S4", VdfEtiquetaFlotante.NombreDePulsador(VdfInterruptoresBuilder.NombreMarcha));
            StringAssert.Contains("I0.1", VdfEtiquetaFlotante.NombreDePulsador(VdfInterruptoresBuilder.NombreS5));
            StringAssert.Contains("I0.2", VdfEtiquetaFlotante.NombreDePulsador(VdfInterruptoresBuilder.NombreS6));
        }

        [Test]
        public void EnElLaboratorio_LosPulsadoresDelTableroDicenQueNoSeUsan()
        {
            StringAssert.Contains("No se usa", VdfEtiquetaFlotante.NombreDePulsador("Pulsador_START"));
            Assert.AreEqual("PARADA DE EMERGENCIA", VdfEtiquetaFlotante.NombreDePulsador("PulsadorHongo_PE"),
                            "la parada de emergencia sí se usa");

            VdfMando.Activo = VdfModoMando.Pulsadores;
            Assert.AreEqual("START", VdfEtiquetaFlotante.NombreDePulsador("Pulsador_START"),
                            "con pulsadores, START es START");
        }
    }
}
