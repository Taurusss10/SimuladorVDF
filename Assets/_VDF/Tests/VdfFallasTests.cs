using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 7: las averías didácticas de la práctica 5.
    ///
    /// Lo que estos tests comprueban no es que la avería «se marque como activa», sino que
    /// **produce de verdad el síntoma que el enunciado describe**. Una avería cuyo texto
    /// prometa un comportamiento que el simulador no reproduce sería peor que no tenerla: el
    /// alumno buscaría un síntoma que no está.
    /// </summary>
    public class VdfFallasTests
    {
        private VfdModel _vfd;
        private PlcFanLogic _plc;
        private VdfWiringGraph _grafo;

        [SetUp]
        public void SetUp()
        {
            _vfd = new VfdModel();
            _vfd.AplicarConfiguracionDeReferencia();
            _plc = new PlcFanLogic();
            _grafo = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(_grafo);
        }

        /// <summary>Lleva el módulo al nivel pedido y devuelve la frecuencia de salida.</summary>
        private float FrecuenciaEnNivel(int nivel)
        {
            _plc.Reiniciar();
            _vfd.ResetFalla();
            _plc.PulsarStart();

            for (int n = 1; n <= nivel; n++)
            {
                _plc.PulsarSubir();
                for (int t = 0; t < 40; t++) Avanzar();
            }
            // Margen para que terminen las rampas del variador.
            for (int t = 0; t < 200; t++) Avanzar();
            return _vfd.FrecuenciaSalidaHz;
        }

        private void Avanzar()
        {
            // Por la puerta, como en la escena: una salida del PLC solo llega al variador si
            // su cable está puesto. Sin esto, las dos averías de cableado del catálogo no
            // tenían ningún efecto sobre el módulo y los tests las daban por buenas igual,
            // porque solo miraban el informe de validación.
            _plc.Tick(0.1f);
            _plc.AplicarA(_vfd, VdfPuertaCableado.Calcular(_grafo));
            _vfd.Tick(0.1f);
        }

        private static VdfFallaDidactica Falla(string id)
        {
            var f = VdfCatalogoFallas.Buscar(id);
            Assert.IsNotNull(f, "No existe la avería " + id);
            return f;
        }

        // =====================================================================================
        // Cada avería produce su síntoma
        // =====================================================================================

        [Test]
        public void SinAverias_LasCuatroVelocidadesSonLasDelManual()
        {
            // Línea base: si esto fallara, los demás tests no demostrarían nada.
            Assert.AreEqual(10f, FrecuenciaEnNivel(1), 0.1f);
            Assert.AreEqual(25f, FrecuenciaEnNivel(2), 0.1f);
            Assert.AreEqual(35f, FrecuenciaEnNivel(3), 0.1f);
            Assert.AreEqual(50f, FrecuenciaEnNivel(4), 0.1f);
        }

        [Test]
        public void H1_01_EnLocalRemote_ImpideQueElMotorArranqueEnCualquierVelocidad()
        {
            var f = Falla("H1-01_LOCAL_REMOTE");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            for (int nivel = 1; nivel <= 4; nivel++)
            {
                Assert.AreEqual(0f, FrecuenciaEnNivel(nivel), 0.01f,
                    "Con H1-01 = 1 el motor no puede arrancar en el nivel " + nivel);
            }

            // Y el PLC sigue haciendo su trabajo: el fallo está en el variador.
            Assert.IsTrue(_plc.SalidaS1, "El PLC sigue cerrando S1; es el variador quien lo ignora.");
        }

        [Test]
        public void H1_05_Y_H1_06_Cruzados_IntercambianLasVelocidades2Y3()
        {
            var f = Falla("H1-05_H1-06_CRUZADOS");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            // El síntoma exacto que promete el texto de la avería.
            Assert.AreEqual(35f, FrecuenciaEnNivel(2), 0.1f,
                "En la velocidad 2 debería salir 35 Hz, la de la 3.");
            Assert.AreEqual(25f, FrecuenciaEnNivel(3), 0.1f,
                "En la velocidad 3 debería salir 25 Hz, la de la 2.");

            // Y las simétricas no se notan, que es lo que la hace difícil de ver.
            Assert.AreEqual(10f, FrecuenciaEnNivel(1), 0.1f);
            Assert.AreEqual(50f, FrecuenciaEnNivel(4), 0.1f);
        }

        [Test]
        public void b1_01_EnAnalogica_DejaSoloLaVelocidad1EnCero()
        {
            var f = Falla("b1-01_ANALOGICA");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            Assert.AreEqual(0f, FrecuenciaEnNivel(1), 0.01f,
                "La velocidad 1 va a buscar la referencia a A1, que no está cableada.");
            Assert.AreEqual(25f, FrecuenciaEnNivel(2), 0.1f, "La 2 sí debe funcionar.");
            Assert.AreEqual(35f, FrecuenciaEnNivel(3), 0.1f);
            Assert.AreEqual(50f, FrecuenciaEnNivel(4), 0.1f);
        }

        [Test]
        public void b1_02_EnOperador_HaceQueElVariadorIgnoreAlPlcPeroObedezcaALaTeclaRun()
        {
            var f = Falla("b1-02_OPERADOR");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            Assert.AreEqual(0f, FrecuenciaEnNivel(4), 0.01f,
                "Con b1-02 = 0 el variador no escucha a los terminales.");

            // La pista que da el enunciado: desde el teclado sí arranca.
            _vfd.MarchaDesdeOperador = true;
            for (int t = 0; t < 200; t++) Avanzar();
            Assert.Greater(_vfd.FrecuenciaSalidaHz, 1f,
                "Debe arrancar desde la tecla RUN: eso es lo que señala a b1-02.");
        }

        // =====================================================================================
        // Averías de cableado
        // =====================================================================================

        [Test]
        public void SinElComunSC_LaValidacionDeCableadoLoDetecta()
        {
            var f = Falla("SC_SIN_COMUN");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            Assert.IsFalse(_grafo.MismaRed("GND24", "SC"));

            var informe = VdfWiringSpec.Validar(_grafo);
            Assert.IsFalse(informe.EsCorrecto);
            Assert.Greater(informe.Faltantes.Count, 0,
                "El informe debe nombrar la unión que falta, que es la pista del alumno.");
        }

        [Test]
        public void SinElCableDeV3_SoloFallaLaSenalizacion()
        {
            var f = Falla("V3_SIN_CABLE");
            Assert.IsTrue(f.Aplicar(_vfd, _grafo));

            Assert.IsFalse(_grafo.MismaRed("PLC-Q0.2", "V3-A"));

            // Lo que la hace didáctica: el control sigue bien, falla solo el aviso.
            Assert.AreEqual(35f, FrecuenciaEnNivel(3), 0.1f,
                "El motor debe seguir yendo a 35 Hz: el fallo es de señalización.");
            Assert.IsTrue(_plc.SalidaV(3), "Y el PLC sigue activando su salida.");
        }

        [Test]
        public void SinElComunSC_NingunaEntradaDelVariadorHaceEfecto()
        {
            // El informe ya lo detectaba, pero el módulo seguía funcionando igual: se quitaba
            // el común y el motor arrancaba y recorría sus cuatro velocidades, que es
            // exactamente lo contrario de lo que la práctica enseña.
            Falla("SC_SIN_COMUN").Aplicar(_vfd, _grafo);

            var puerta = VdfPuertaCableado.Calcular(_grafo);
            Assert.IsFalse(puerta.ComunDeEntradas, "sin común no hay retorno para las entradas");
            Assert.IsFalse(puerta.S1, "y por tanto S1 no llega");

            Assert.AreEqual(0f, FrecuenciaEnNivel(1), 0.1f, "el motor no se mueve");
            Assert.AreEqual(0f, FrecuenciaEnNivel(4), 0.1f, "en ninguna velocidad");
        }

        [Test]
        public void SinElCableDeV3_ElPilotoSeQuedaApagadoAunqueElPlcLoMande()
        {
            Falla("V3_SIN_CABLE").Aplicar(_vfd, _grafo);

            Assert.AreEqual(35f, FrecuenciaEnNivel(3), 0.1f, "el control sigue bien");
            Assert.IsTrue(_plc.SalidaV(3), "y el PLC sigue mandando encender V3");

            var puerta = VdfPuertaCableado.Calcular(_grafo);
            Assert.IsFalse(puerta.Piloto(3), "pero sin su cable, el piloto no puede encenderse");
            Assert.IsTrue(puerta.Piloto(2), "los demás sí");
        }

        // =====================================================================================
        // Provocar y reparar
        // =====================================================================================

        [Test]
        public void TodaAveria_SeDetectaMientrasEstaYDejaDeDetectarseAlRepararla()
        {
            foreach (var f in VdfCatalogoFallas.Crear())
            {
                SetUp();   // módulo sano

                Assert.IsFalse(f.EstaPresente(_vfd, _grafo),
                    "Con el módulo sano, " + f.Id + " no debería detectarse.");

                Assert.IsTrue(f.Aplicar(_vfd, _grafo), "No se pudo provocar " + f.Id);
                Assert.IsTrue(f.EstaPresente(_vfd, _grafo),
                    "Tras provocarla, " + f.Id + " debería detectarse.");

                Reparar(f);

                Assert.IsTrue(f.Reparada(_vfd, _grafo),
                    "Tras repararla, " + f.Id + " no debería seguir detectándose.");
            }
        }

        private void Reparar(VdfFallaDidactica f)
        {
            switch (f.Tipo)
            {
                case VdfTipoFalla.ParametroMal:
                    _vfd.EscribirParametro(f.ObjetivoA, f.ValorCorrectoA);
                    break;
                case VdfTipoFalla.ParametrosIntercambiados:
                    _vfd.EscribirParametro(f.ObjetivoA, f.ValorCorrectoA);
                    _vfd.EscribirParametro(f.ObjetivoB, f.ValorCorrectoB);
                    break;
                case VdfTipoFalla.CableFaltante:
                    _grafo.Conectar(f.ObjetivoA, f.ObjetivoB);
                    break;
            }
        }

        [Test]
        public void DejarLosDosParametrosIguales_NoCuentaComoReparado()
        {
            var f = Falla("H1-05_H1-06_CRUZADOS");
            f.Aplicar(_vfd, _grafo);

            // Un alumno podría «arreglarlo» poniendo los dos a 3. Sigue estando mal, y la
            // comprobación tiene que exigir que cada uno recupere SU valor.
            _vfd.EscribirParametro("H1-05", 3f);
            _vfd.EscribirParametro("H1-06", 3f);

            Assert.IsTrue(f.EstaPresente(_vfd, _grafo),
                "Con los dos en 3 la avería sigue ahí: falta la multi-velocidad 2.");
        }

        // =====================================================================================
        // Coherencia del catálogo
        // =====================================================================================

        [Test]
        public void TodaAveria_TieneIdUnicoSintomaYExplicacion()
        {
            var vistos = new HashSet<string>();
            foreach (var f in VdfCatalogoFallas.Crear())
            {
                Assert.IsTrue(vistos.Add(f.Id), "Id de avería repetido: " + f.Id);
                Assert.IsFalse(string.IsNullOrEmpty(f.Titulo));
                Assert.IsFalse(string.IsNullOrEmpty(f.Sintoma), f.Id + " no describe su síntoma");
                Assert.IsFalse(string.IsNullOrEmpty(f.Explicacion), f.Id + " no se explica");
            }
        }

        [Test]
        public void TodoParametroYBorneCitadoPorUnaAveria_Existe()
        {
            var tablero = VdfPanelLayoutDefaults.Crear();
            var plc = VdfPanelLayoutDefaults.CrearPlc();
            try
            {
                var bornes = new HashSet<string>();
                foreach (var e in tablero.Elementos) if (!string.IsNullOrEmpty(e.Id)) bornes.Add(e.Id);
                foreach (var e in plc.Elementos) if (!string.IsNullOrEmpty(e.Id)) bornes.Add(e.Id);
                bornes.Add("MOT-U"); bornes.Add("MOT-V"); bornes.Add("MOT-W");

                foreach (var f in VdfCatalogoFallas.Crear())
                {
                    if (f.Tipo == VdfTipoFalla.CableFaltante)
                    {
                        Assert.IsTrue(bornes.Contains(f.ObjetivoA), f.Id + ": no existe " + f.ObjetivoA);
                        Assert.IsTrue(bornes.Contains(f.ObjetivoB), f.Id + ": no existe " + f.ObjetivoB);
                    }
                    else
                    {
                        VfdParameterDef def;
                        Assert.IsTrue(VfdParameterTable.TryGet(f.ObjetivoA, out def),
                            f.Id + ": no existe el parámetro " + f.ObjetivoA);
                        if (!string.IsNullOrEmpty(f.ObjetivoB))
                            Assert.IsTrue(VfdParameterTable.TryGet(f.ObjetivoB, out def),
                                f.Id + ": no existe el parámetro " + f.ObjetivoB);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tablero);
                Object.DestroyImmediate(plc);
            }
        }

        // =====================================================================================
        // La práctica 5
        // =====================================================================================

        [Test]
        public void LaPractica5_TieneGuionCoherenteYAvisosDeSeguridad()
        {
            var p = VdfPracticasDefaults.CrearSeguridadYFallas();
            try
            {
                Assert.AreEqual(0, p.Validar().Count,
                    string.Join("\n  ", p.Validar().ToArray()));

                int conAviso = 0;
                foreach (var paso in p.Pasos) if (paso.TieneAdvertencia) conAviso++;

                Assert.GreaterOrEqual(conAviso, 3,
                    "Una práctica de seguridad tiene que avisar antes de los pasos peligrosos.");
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void LaPractica5_SeCompletaDiagnosticandoYReparandoLaAveria()
        {
            var practica = VdfPracticasDefaults.CrearSeguridadYFallas();
            try
            {
                var ctx = new VdfContextoPractica();
                ctx.Vfd = _vfd;
                ctx.Plc = _plc;
                ctx.Grafo = _grafo;
                ctx.FallaActiva = Falla("H1-01_LOCAL_REMOTE");
                ctx.FallaActiva.Aplicar(_vfd, _grafo);

                // Pasos manuales: el alumno los confirma al leerlos.
                foreach (var paso in practica.Pasos)
                    if (paso.Condicion == VdfCondicion.Manual)
                        ctx.Identificar(VdfEvaluadorPasos.ClaveManual(paso));

                // Operar: arrancar, parar de emergencia y rearmar.
                _plc.PulsarStart(); ctx.ActualizarLogros();
                _plc.PulsarSubir();
                for (int t = 0; t < 40; t++) { Avanzar(); ctx.ActualizarLogros(); }
                _plc.PulsarEmergencia(); _plc.AplicarA(_vfd); ctx.ActualizarLogros();
                ctx.EmergenciaConMotorGirando = true;

                Assert.AreNotEqual(VfdFault.Ninguna, _vfd.Falla,
                    "La seta debe dejar el variador en falla, no solo parado.");

                _plc.RearmarEmergencia(); ctx.ActualizarLogros();
                _vfd.ResetFalla();

                // Diagnosticar y reparar.
                ctx.Identificar(ctx.FallaActiva.Id);
                _vfd.EscribirParametro("H1-01", 40f);
                ctx.NivelMaximoTrasReparar = PlcFanLogic.NivelMaximo;   // y se probó, ver arriba

                for (int i = 0; i < practica.TotalPasos; i++)
                {
                    Assert.IsTrue(VdfEvaluadorPasos.Cumple(practica.Paso(i), ctx),
                        "Paso " + (i + 1) + " sin cumplir: " + practica.Paso(i).Instruccion);
                }
            }
            finally
            {
                Object.DestroyImmediate(practica);
            }
        }

        [Test]
        public void CadaAveria_DiceComoRepararla()
        {
            foreach (var f in VdfCatalogoFallas.Crear())
            {
                StringAssert.Contains("CÓMO REPARARLO:", f.Explicacion, f.Id + " no dice cómo repararla");
                StringAssert.StartsWith("<b>CÓMO REPARARLO:", VdfPanelDiagnostico.ExplicacionParaElPanel(f.Explicacion),
                    f.Id + ": en el panel el arreglo tiene que ir delante, donde no se corta");
            }
        }

        [Test]
        public void LasSeisAverias_SalenAntesDeRepetirse()
        {
            VdfCatalogoFallas.Barajar();
            int total = VdfCatalogoFallas.Crear().Count;
            var vistas = new HashSet<string>();
            for (int i = 0; i < total; i++)
                Assert.IsTrue(vistas.Add(VdfCatalogoFallas.Elegir().Id), "se repitió una avería antes de salir todas");
            Assert.AreEqual(total, vistas.Count);

            // Al cambiar de ronda no se repite la última.
            for (int ronda = 0; ronda < 20; ronda++)
            {
                VdfCatalogoFallas.Barajar();
                string ultima = null;
                for (int i = 0; i < total; i++) ultima = VdfCatalogoFallas.Elegir().Id;
                Assert.AreNotEqual(ultima, VdfCatalogoFallas.Elegir().Id, "la ronda nueva empezó repitiendo la última");
            }
            VdfCatalogoFallas.Barajar();
        }
    }
}
