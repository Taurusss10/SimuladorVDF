using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    public class VdfPracticasTests
    {
        private readonly List<VdfPractica> _creadas = new List<VdfPractica>();

        private VdfPractica Registrar(VdfPractica p)
        {
            _creadas.Add(p);
            return p;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in _creadas) if (p != null) Object.DestroyImmediate(p);
            _creadas.Clear();
        }

        // =====================================================================================
        // Los guiones son coherentes
        // =====================================================================================

        [Test]
        public void LasCuatroPracticas_NoTienenProblemasEnSuGuion()
        {
            foreach (var p in VdfPracticasDefaults.CrearTodas())
            {
                Registrar(p);
                var problemas = p.Validar();

                Assert.AreEqual(0, problemas.Count,
                    "Práctica " + p.Numero + " (" + p.Titulo + "):\n  " +
                    string.Join("\n  ", problemas.ToArray()));
            }
        }

        [Test]
        public void LasPracticas_EstanNumeradasDel1Al5YTienenPasos()
        {
            var todas = VdfPracticasDefaults.CrearTodas();
            foreach (var p in todas) Registrar(p);

            Assert.AreEqual(5, todas.Count, "Están implementadas las cinco prácticas.");
            for (int i = 0; i < todas.Count; i++)
            {
                Assert.AreEqual(i + 1, todas[i].Numero);
                Assert.Greater(todas[i].TotalPasos, 0, "La práctica " + todas[i].Numero + " no tiene pasos.");
                Assert.IsFalse(string.IsNullOrEmpty(todas[i].Titulo));
            }
        }

        [Test]
        public void TodoBorneCitadoEnLaPracticaDeCableado_ExisteEnAlgunLayout()
        {
            // Es la costura entre el guion y el tablero, y es justo donde una errata pasaría
            // desapercibida: el paso nunca se cumpliría y nadie sabría por qué.
            var tablero = VdfPanelLayoutDefaults.Crear();
            var plc = VdfPanelLayoutDefaults.CrearPlc();
            try
            {
                var ids = new HashSet<string>();
                foreach (var e in tablero.Elementos) if (!string.IsNullOrEmpty(e.Id)) ids.Add(e.Id);
                foreach (var e in plc.Elementos) if (!string.IsNullOrEmpty(e.Id)) ids.Add(e.Id);

                // Los bornes del motor no salen de un layout: los crea el builder como
                // borneras sueltas sobre el propio motor. Para el grafo son iguales que
                // los demás, pero no están en ningún asset de layout.
                ids.Add("MOT-U"); ids.Add("MOT-V"); ids.Add("MOT-W");

                var practica = Registrar(VdfPracticasDefaults.CrearCableado());

                foreach (var paso in practica.Pasos)
                {
                    if (paso.Condicion != VdfCondicion.BornesUnidos) continue;

                    var partes = paso.Objetivo.Split('|');
                    foreach (var parte in partes)
                    {
                        string id = parte.Trim();
                        Assert.IsTrue(ids.Contains(id),
                            "El paso \"" + paso.Instruccion + "\" cita el borne '" + id +
                            "', que no existe en ningún layout.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tablero);
                Object.DestroyImmediate(plc);
            }
        }

        [Test]
        public void TodaUnionQuePideLaGuia_FormaParteDelCableadoCorrecto()
        {
            // La otra costura, y más traicionera que la de los nombres: un paso puede citar
            // dos bornes que existen y aun así pedir una unión que el validador considera
            // cable de más. El alumno haría lo que la guía le dice y el panel le diría que
            // sobra un cable, sin forma de salir del bucle.
            var redes = VdfWiringSpec.Practica();
            var practica = Registrar(VdfPracticasDefaults.CrearCableado());

            foreach (var paso in practica.Pasos)
            {
                if (paso.Condicion != VdfCondicion.BornesUnidos) continue;

                var partes = paso.Objetivo.Split('|');
                Assert.AreEqual(2, partes.Length,
                    "El objetivo \"" + paso.Objetivo + "\" debería ser \"A|B\".");

                string a = partes[0].Trim(), b = partes[1].Trim();
                bool juntos = false;
                foreach (var red in redes)
                {
                    bool tieneA = System.Array.IndexOf(red.Bornes, a) >= 0;
                    bool tieneB = System.Array.IndexOf(red.Bornes, b) >= 0;
                    if (tieneA && tieneB) { juntos = true; break; }
                }

                Assert.IsTrue(juntos,
                    "El paso \"" + paso.Instruccion + "\" pide unir " + a + " con " + b +
                    ", pero no hay ninguna red del cableado correcto que los contenga a los dos: " +
                    "seguir la guía dejaría un cable marcado como sobrante.");
            }
        }

        [Test]
        public void TodoGrupoCitadoPorUnaPractica_ExisteEnElLayout()
        {
            // La otra costura entre el guion y el tablero. Al escribir la práctica 1 se puso
            // el grupo "Velocidad", que no existe: el real es "Indicadores de velocidad". El
            // paso habría quedado imposible de cumplir sin que nada avisara.
            var tablero = VdfPanelLayoutDefaults.Crear();
            try
            {
                var grupos = new HashSet<string>();
                foreach (var e in tablero.Elementos)
                    if (!string.IsNullOrEmpty(e.Grupo)) grupos.Add(e.Grupo);

                foreach (var p in VdfPracticasDefaults.CrearTodas())
                {
                    Registrar(p);
                    foreach (var paso in p.Pasos)
                    {
                        if (paso.Condicion != VdfCondicion.GrupoIdentificado) continue;

                        Assert.IsTrue(grupos.Contains(paso.Objetivo),
                            "La práctica " + p.Numero + " pide identificar el grupo '" +
                            paso.Objetivo + "', que no existe en el layout. Grupos reales: " +
                            string.Join(", ", new List<string>(grupos).ToArray()));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tablero);
            }
        }

        [Test]
        public void TodoComponenteCitadoPorUnaPractica_ExisteEnElLayout()
        {
            var tablero = VdfPanelLayoutDefaults.Crear();
            var plc = VdfPanelLayoutDefaults.CrearPlc();
            try
            {
                var ids = new HashSet<string>();
                foreach (var e in tablero.Elementos) if (!string.IsNullOrEmpty(e.Id)) ids.Add(e.Id);
                foreach (var e in plc.Elementos) if (!string.IsNullOrEmpty(e.Id)) ids.Add(e.Id);
                ids.Add("MOT-U"); ids.Add("MOT-V"); ids.Add("MOT-W");

                foreach (var p in VdfPracticasDefaults.CrearTodas())
                {
                    Registrar(p);
                    foreach (var paso in p.Pasos)
                    {
                        if (paso.Condicion != VdfCondicion.ComponenteIdentificado) continue;

                        Assert.IsTrue(ids.Contains(paso.Objetivo),
                            "La práctica " + p.Numero + " pide identificar '" + paso.Objetivo +
                            "', que no existe en ningún layout.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tablero);
                Object.DestroyImmediate(plc);
            }
        }

        [Test]
        public void TodoPasoTieneInstruccion()
        {
            foreach (var p in VdfPracticasDefaults.CrearTodas())
            {
                Registrar(p);
                foreach (var paso in p.Pasos)
                    Assert.IsFalse(string.IsNullOrEmpty(paso.Instruccion),
                        "Un paso de la práctica " + p.Numero + " no dice qué hacer.");
            }
        }

        [Test]
        public void ValidarDetectaUnParametroInventado()
        {
            // El validador tiene que saltar de verdad, no limitarse a devolver lista vacía.
            var p = Registrar(ScriptableObject.CreateInstance<VdfPractica>());
            p.Pasos = new List<VdfPasoPractica>
            {
                new VdfPasoPractica
                {
                    Instruccion = "Programar un parámetro que no existe",
                    Condicion = VdfCondicion.ParametroEnValor,
                    Objetivo = "Z9-99",
                    Valor = 1f
                }
            };

            Assert.AreEqual(1, p.Validar().Count);
        }

        [Test]
        public void ValidarDetectaUnPasoDeUnionMalEscrito()
        {
            var p = Registrar(ScriptableObject.CreateInstance<VdfPractica>());
            p.Pasos = new List<VdfPasoPractica>
            {
                new VdfPasoPractica
                {
                    Instruccion = "Unir algo",
                    Condicion = VdfCondicion.BornesUnidos,
                    Objetivo = "SOLO_UN_BORNE"      // falta el separador |
                }
            };

            Assert.AreEqual(1, p.Validar().Count);
        }

        // =====================================================================================
        // El evaluador
        // =====================================================================================

        private static VdfContextoPractica ContextoBasico()
        {
            var ctx = new VdfContextoPractica();
            ctx.Vfd = new VfdModel();
            ctx.Plc = new PlcFanLogic();
            ctx.Grafo = new VdfWiringGraph();
            return ctx;
        }

        [Test]
        public void ParametroEnValor_SeCumpleAlProgramarlo()
        {
            var ctx = ContextoBasico();
            var paso = new VdfPasoPractica
            {
                Instruccion = "d1-02 = 25 Hz",
                Condicion = VdfCondicion.ParametroEnValor,
                Objetivo = "d1-02",
                Valor = 25f
            };

            Assert.IsFalse(VdfEvaluadorPasos.Cumple(paso, ctx));
            ctx.Vfd.EscribirParametro("d1-02", 25f);
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));
        }

        [Test]
        public void BornesUnidos_NoDependeDelOrdenNiDelCamino()
        {
            var ctx = ContextoBasico();
            var paso = new VdfPasoPractica
            {
                Instruccion = "Unir A con C",
                Condicion = VdfCondicion.BornesUnidos,
                Objetivo = "A|C"
            };

            Assert.IsFalse(VdfEvaluadorPasos.Cumple(paso, ctx));

            // A través de B: eléctricamente es lo mismo que un cable directo, y el simulador
            // no debe suspender un cableado que en el tablero real funciona.
            ctx.Grafo.Conectar("A", "B");
            ctx.Grafo.Conectar("B", "C");

            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));
        }

        [Test]
        public void GrupoIdentificado_ExigeTodosSusMiembros()
        {
            var ctx = ContextoBasico();
            ctx.Grupos["Velocidad"] = new List<string> { "V1", "V2", "V3", "V4" };

            var paso = new VdfPasoPractica
            {
                Instruccion = "Identifica V1 a V4",
                Condicion = VdfCondicion.GrupoIdentificado,
                Objetivo = "Velocidad"
            };

            ctx.Identificar("V1");
            ctx.Identificar("V2");
            Assert.IsFalse(VdfEvaluadorPasos.Cumple(paso, ctx));

            int total;
            Assert.AreEqual(2, VdfEvaluadorPasos.ProgresoDeGrupo("Velocidad", ctx, out total));
            Assert.AreEqual(4, total);

            ctx.Identificar("V3");
            ctx.Identificar("V4");
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));
        }

        // =====================================================================================
        // Condiciones y logros no son lo mismo
        // =====================================================================================

        [Test]
        public void UnLogro_NoSeDeshaceAlCambiarElEstado()
        {
            var ctx = ContextoBasico();
            var paso = new VdfPasoPractica
            {
                Instruccion = "Sube hasta la velocidad 4",
                Condicion = VdfCondicion.NivelAlcanzado,
                Valor = 4f
            };

            Assert.IsTrue(paso.EsLogro);

            ctx.Plc.PulsarStart();
            for (int i = 0; i < 4; i++)
            {
                ctx.Plc.PulsarSubir();
                for (int t = 0; t < 40; t++) { ctx.Plc.Tick(0.1f); ctx.ActualizarLogros(); }
            }
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));

            for (int i = 0; i < 3; i++)
            {
                ctx.Plc.PulsarBajar();
                for (int t = 0; t < 40; t++) { ctx.Plc.Tick(0.1f); ctx.ActualizarLogros(); }
            }
            Assert.AreEqual(1, ctx.Plc.NivelActivo);
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx),
                "Bajar de velocidad no puede desmarcar un paso ya conseguido.");
        }

        [Test]
        public void UnaCondicion_SiSeDeshaceAlRomperLoQueComprueba()
        {
            var ctx = ContextoBasico();
            var paso = new VdfPasoPractica
            {
                Instruccion = "El variador tiene tensión",
                Condicion = VdfCondicion.VariadorConTension
            };

            Assert.IsFalse(paso.EsLogro);
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));

            ctx.Vfd.SetAlimentacion(false);
            Assert.IsFalse(VdfEvaluadorPasos.Cumple(paso, ctx),
                "Quitar la tensión sí debe desmarcar el paso: es una condición, no un logro.");
        }

        [Test]
        public void PararAntesDeOperar_NoCuentaComoHaberParadoTrasOperar()
        {
            var ctx = ContextoBasico();

            ctx.Plc.PulsarStart();
            ctx.ActualizarLogros();
            ctx.Plc.PulsarStop();
            ctx.ActualizarLogros();

            Assert.IsFalse(ctx.ParoDespuesDeOperar,
                "Pulsar STOP sin haber operado no debe dar por hecho el último paso.");
        }

        // =====================================================================================
        // El test que importa: recorrer la práctica 4 entera
        // =====================================================================================

        [Test]
        public void LaPractica4_SeCompletaRecorriendolaDePuntaAPunta()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearOperacion());

            var ctx = ContextoBasico();
            ctx.Vfd.AplicarConfiguracionDeReferencia();

            System.Func<int> primerPasoSinCumplir = delegate()
            {
                for (int i = 0; i < practica.TotalPasos; i++)
                    if (!VdfEvaluadorPasos.Cumple(practica.Paso(i), ctx)) return i;
                return -1;
            };

            System.Action<float> avanzar = delegate(float segundos)
            {
                int pasos = Mathf.RoundToInt(segundos / 0.1f);
                for (int t = 0; t < pasos; t++)
                {
                    ctx.Plc.Tick(0.1f);
                    ctx.Plc.AplicarA(ctx.Vfd);
                    ctx.Vfd.Tick(0.1f);
                    ctx.ActualizarLogros();
                }
            };

            Assert.AreEqual(1, primerPasoSinCumplir(),
                "Al empezar solo debería estar cumplido el paso de la tensión.");

            ctx.Plc.PulsarStart();
            avanzar(0.2f);

            for (int nivel = 1; nivel <= 4; nivel++)
            {
                ctx.Plc.PulsarSubir();
                avanzar(4f);
                Assert.AreEqual(nivel, ctx.Plc.NivelActivo, "No se alcanzó el nivel " + nivel);
            }

            // Las cuatro velocidades del manual, de punta a punta.
            Assert.AreEqual(50f, ctx.Vfd.ReferenciaFrecuenciaHz, 0.01f);

            for (int i = 0; i < 3; i++) { ctx.Plc.PulsarBajar(); avanzar(4f); }
            Assert.AreEqual(1, ctx.Plc.NivelActivo);

            ctx.Plc.PulsarStop();
            avanzar(0.2f);

            Assert.AreEqual(-1, primerPasoSinCumplir(),
                "Tras recorrer la práctica completa no debería quedar ningún paso sin cumplir.");
        }

        [Test]
        public void LaPractica2_SeCompletaProgramandoLaTablaDeReferencia()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearConfiguracion());

            var ctx = ContextoBasico();

            // Inicializar es un paso real; y los de «entra a ver» exigen haber entrado.
            ctx.VariadorInicializado = true;
            ctx.Vfd.AplicarConfiguracionDeReferencia();
            foreach (var paso in practica.Pasos)
                if (!string.IsNullOrEmpty(paso.Objetivo)) ctx.ParametrosVistos.Add(paso.Objetivo);

            for (int i = 0; i < practica.TotalPasos; i++)
            {
                Assert.IsTrue(VdfEvaluadorPasos.Cumple(practica.Paso(i), ctx),
                    "Paso " + (i + 1) + " sin cumplir con la configuración de referencia aplicada: " +
                    practica.Paso(i).Instruccion);
            }
        }

        [Test]
        public void LaPractica3_SeCompletaConElCableadoCorrecto()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearCableado());

            var ctx = ContextoBasico();
            ctx.Identificar(VdfEvaluadorPasos.ClaveManual(practica.Paso(0)));

            VdfWiringSpec.AplicarCableadoCorrecto(ctx.Grafo);
            ctx.InformeCableado = VdfWiringSpec.Validar(ctx.Grafo);

            for (int i = 0; i < practica.TotalPasos; i++)
            {
                Assert.IsTrue(VdfEvaluadorPasos.Cumple(practica.Paso(i), ctx),
                    "Paso " + (i + 1) + " sin cumplir con el cableado correcto aplicado: " +
                    practica.Paso(i).Instruccion);
            }
        }

        [Test]
        public void LaPractica2_AvisaDeLosPasosPeligrosos()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearConfiguracion());

            // Inicializar borra la configuración del equipo: tiene que avisar antes.
            Assert.IsTrue(practica.Paso(0).TieneAdvertencia,
                "El paso de inicializar debe llevar advertencia: borra la configuración anterior.");
        }

        [Test]
        public void EnElReconocimiento_CadaPiezaSenaladaExplicaQueEs()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearReconocimiento());

            for (int i = 0; i < practica.TotalPasos; i++)
            {
                var paso = practica.Paso(i);
                if (paso.Condicion == VdfCondicion.Manual) continue;
                Assert.IsTrue(paso.TieneExplicacion,
                    "el paso " + (i + 1) + " se acierta sin explicar qué es: " + paso.Instruccion);
            }
        }

        /// <summary>
        /// La ficha que enseña la guía es la del último paso cumplido que la tenga, y se
        /// queda puesta mientras se busca la siguiente pieza.
        /// </summary>
        [Test]
        public void LaFichaDeLaGuia_EsLaDelUltimoPasoCumplido()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearReconocimiento());

            Assert.IsNull(VdfPanelGuia.UltimaFicha(practica, 0), "al empezar no hay ficha");
            Assert.IsNull(VdfPanelGuia.UltimaFicha(practica, 1),
                "el paso 1 es solo mirar: no trae ficha");
            Assert.AreSame(practica.Paso(1), VdfPanelGuia.UltimaFicha(practica, 2),
                "tras señalar el breaker, la ficha es la del breaker");
            Assert.AreSame(practica.Paso(practica.TotalPasos - 2),
                VdfPanelGuia.UltimaFicha(practica, -1),
                "con la práctica terminada queda la última ficha");
        }

        /// <summary>
        /// Los pilotos FALLA y E del tablero real son azules. La guía decía «rojo» y
        /// «ámbar», copiado del manual del módulo, y el alumno los buscaba sin encontrarlos.
        /// </summary>
        [Test]
        public void LaGuia_NoDescribeColoresQueElTableroNoTiene()
        {
            foreach (var practica in VdfPracticasDefaults.CrearTodas())
            {
                Registrar(practica);
                for (int i = 0; i < practica.TotalPasos; i++)
                {
                    var paso = practica.Paso(i);
                    string texto = (paso.Instruccion + " " + paso.Pista).ToLowerInvariant();
                    StringAssert.DoesNotContain("ámbar", texto, "práctica " + practica.Numero + " paso " + (i + 1));
                    StringAssert.DoesNotContain("palanca roja", texto, "práctica " + practica.Numero + " paso " + (i + 1));
                    StringAssert.DoesNotContain("falla en rojo", texto, "práctica " + practica.Numero + " paso " + (i + 1));
                }
            }
        }

        [Test]
        public void EntrarAVerUnParametro_EsLoQueCumpleElPaso_NoQueYaValgaBien()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearConfiguracion());
            var ctx = ContextoBasico();
            ctx.Vfd.AplicarConfiguracionDeReferencia();

            VdfPasoPractica e104 = null;
            foreach (var paso in practica.Pasos) if (paso.Objetivo == "E1-04") e104 = paso;
            Assert.IsNotNull(e104);
            Assert.AreEqual(VdfCondicion.ParametroRevisado, e104.Condicion);

            Assert.IsFalse(VdfEvaluadorPasos.Cumple(e104, ctx), "vale 60 de fábrica, pero nadie entró a verlo");
            ctx.ParametrosVistos.Add("E1-04");
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(e104, ctx));
        }

        /// <summary>Inicializar ya no se pasa con el botón de LISTO: hay que hacerlo.</summary>
        [Test]
        public void ElPrimerPasoDeLaPractica2_ExigeInicializarDeVerdad()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearConfiguracion());
            var ctx = ContextoBasico();
            Assert.AreEqual(VdfCondicion.VariadorInicializado, practica.Paso(0).Condicion);
            Assert.IsFalse(VdfEvaluadorPasos.Cumple(practica.Paso(0), ctx));
            ctx.VariadorInicializado = true;
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(practica.Paso(0), ctx));
        }

        /// <summary>El teclado apunta en qué parámetros se ha entrado con ENTER.</summary>
        [Test]
        public void ElTeclado_ApuntaLosParametrosEnLosQueSeEntra()
        {
            var vfd = new VfdModel();
            var teclado = new VfdKeypad(vfd);
            Assert.IsTrue(teclado.IrAParametro("E1-04"));
            Assert.IsFalse(teclado.ParametrosVistos.Contains("E1-04"), "solo con mirarlo de pasada no cuenta");
            teclado.Pulsar(VfdKey.Enter);
            Assert.IsTrue(teclado.ParametrosVistos.Contains("E1-04"));
            teclado.OlvidarVistos();
            Assert.AreEqual(0, teclado.ParametrosVistos.Count);
        }

        [Test]
        public void SenalarAntesDeTiempo_NoAdelantaPasos()
        {
            var go = new GameObject("RunnerPrueba");
            try
            {
                var runner = go.AddComponent<VdfPracticaRunner>();
                runner.Practica = Registrar(VdfPracticasDefaults.CrearReconocimiento());
                runner.ConfirmarPasoManual();
                Assert.AreEqual(1, runner.PasoActual(), "toca señalar el breaker");

                runner.Identificar("R/L1");       // la del paso siguiente, antes de tiempo
                Assert.AreEqual(1, runner.PasoActual(), "señalar R/L1 no puede saltarse el breaker");

                runner.Identificar("BRK-L1");
                Assert.AreEqual(2, runner.PasoActual());
                Assert.IsFalse(runner.PasoCumplido(2), "R/L1 se señaló antes de tiempo: no cuenta");

                runner.Identificar("R/L1");
                Assert.AreEqual(3, runner.PasoActual());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LaSetaConElMotorParado_NoCumpleElPaso()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearSeguridadYFallas());
            VdfPasoPractica seta = null;
            foreach (var paso in practica.Pasos)
                if (paso.Instruccion.StartsWith("Con el motor girando")) seta = paso;
            Assert.IsNotNull(seta);
            Assert.AreEqual(VdfCondicion.EmergenciaConMotorGirando, seta.Condicion);

            var ctx = ContextoBasico();
            ctx.EmergenciaAccionada = true;
            Assert.IsFalse(VdfEvaluadorPasos.Cumple(seta, ctx), "seta con el motor parado");
            ctx.EmergenciaConMotorGirando = true;
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(seta, ctx));
        }

        /// <summary>
        /// «Repara y comprueba las cuatro velocidades» exige, tras reparar, volver a llegar a
        /// la velocidad 4. Reparar a secas lo cumplía: «me deja continuar de manera muy
        /// permisiva».
        /// </summary>
        [Test]
        public void RepararSinProbar_NoCumpleElPaso()
        {
            var practica = Registrar(VdfPracticasDefaults.CrearSeguridadYFallas());
            VdfPasoPractica reparar = null;
            foreach (var paso in practica.Pasos)
                if (paso.Condicion == VdfCondicion.FallaReparadaYProbada) reparar = paso;
            Assert.IsNotNull(reparar, "la práctica 5 tiene que pedir reparar Y probar");

            var ctx = ContextoBasico();
            ctx.Vfd.AplicarConfiguracionDeReferencia();
            ctx.FallaActiva = VdfCatalogoFallas.Crear().Find(f => f.Id == "b1-02_OPERADOR");
            Assert.IsNotNull(ctx.FallaActiva);

            Assert.IsFalse(VdfEvaluadorPasos.Cumple(reparar, ctx), "reparada pero sin probar");
            ctx.NivelMaximoTrasReparar = PlcFanLogic.NivelMaximo;
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(reparar, ctx));
        }

        [Test]
        public void ConLaMarchaBloqueada_RunNoArrancaElMotor()
        {
            var vfd = new VfdModel();
            var teclado = new VfdKeypad(vfd);
            teclado.Pulsar(VfdKey.LoRe);
            teclado.Pulsar(VfdKey.Run);
            Assert.IsTrue(vfd.OrdenDeMarchaActiva(), "en LOCAL, RUN tiene que dar marcha (como el equipo real)");

            vfd.MarchaBloqueada = true;
            Assert.IsFalse(vfd.OrdenDeMarchaActiva(), "bloqueada, no puede haber marcha");
            Assert.IsTrue(vfd.PideMarcha(), "pero el intento se tiene que poder ver, para avisar");
        }

        [Test]
        public void LasPracticasDeReconocimientoYConfiguracion_NoDejanArrancarElMotor()
        {
            Assert.IsTrue(VdfPracticasDefaults.CrearReconocimiento().BloqueaMarcha);
            Assert.IsTrue(VdfPracticasDefaults.CrearConfiguracion().BloqueaMarcha);
            Assert.IsFalse(VdfPracticasDefaults.CrearCableado().BloqueaMarcha);
            Assert.IsFalse(VdfPracticasDefaults.CrearOperacion().BloqueaMarcha);
            Assert.IsFalse(VdfPracticasDefaults.CrearSeguridadYFallas().BloqueaMarcha);
        }

        [Test]
        public void ElPasoDeSinAvisos_DiceQueParametroFalta()
        {
            var go = new GameObject("RunnerPrueba");
            try
            {
                var runner = go.AddComponent<VdfPracticaRunner>();
                var p = Registrar(ScriptableObject.CreateInstance<VdfPractica>());
                p.Pasos.Add(new VdfPasoPractica { Instruccion = "Sin avisos", Condicion = VdfCondicion.ConfiguracionSinAvisos });
                runner.Practica = p;

                var vfd = new VfdModel();
                vfd.EscribirParametro("H1-01", 1f);
                runner.Contexto.Vfd = vfd;

                string detalle = runner.DetalleDelPasoActual();
                StringAssert.Contains("H1-01", detalle, "la guía no dice qué parámetro queda mal");
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void SenalarLoQueNoEs_LaGuiaDiceQueEsYQueNoEsLoPedido()
        {
            var go = new GameObject("RunnerPrueba");
            try
            {
                var runner = go.AddComponent<VdfPracticaRunner>();
                runner.Practica = Registrar(VdfPracticasDefaults.CrearReconocimiento());
                runner.ConfirmarPasoManual();
                Assert.IsNull(runner.AvisoDeSenaladoErroneo());

                runner.Identificar("BRK-L3");
                string aviso = runner.DetalleDelPasoActual();
                StringAssert.Contains("F2", aviso, "dice qué se ha señalado");
                StringAssert.Contains("no lo que se pide", aviso);
                Assert.AreEqual(1, runner.PasoActual(), "no avanza");

                runner.Identificar("BRK-L1");
                Assert.AreEqual(2, runner.PasoActual());
                Assert.IsNull(runner.AvisoDeSenaladoErroneo(), "al acertar, el aviso se va");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SinCablesSobrantes_NoSeCumpleConElTableroVacio()
        {
            var ctx = new VdfContextoPractica();
            var paso = new VdfPasoPractica { Condicion = VdfCondicion.SinCablesSobrantes };

            var vacio = new VdfWiringGraph();
            ctx.InformeCableado = VdfWiringSpec.Validar(vacio, VdfWiringSpec.Laboratorio());
            Assert.IsFalse(VdfEvaluadorPasos.Cumple(paso, ctx), "con el tablero vacío no está hecho");

            var completo = new VdfWiringGraph();
            VdfWiringSpec.AplicarCableadoCorrecto(completo, VdfWiringSpec.Laboratorio());
            ctx.InformeCableado = VdfWiringSpec.Validar(completo, VdfWiringSpec.Laboratorio());
            Assert.IsTrue(VdfEvaluadorPasos.Cumple(paso, ctx));
        }

        [Test]
        public void UnCableMalPuesto_LaGuiaLoDiceEnElPasoDeUnir()
        {
            var go = new GameObject("RunnerPrueba");
            try
            {
                var runner = go.AddComponent<VdfPracticaRunner>();
                var p = Registrar(ScriptableObject.CreateInstance<VdfPractica>());
                p.Pasos.Add(new VdfPasoPractica { Instruccion = "F2 a S/L2", Condicion = VdfCondicion.BornesUnidos, Objetivo = "BRK-L3|S/L2" });
                runner.Practica = p;

                var g = new VdfWiringGraph();
                g.Conectar("BRK-L2", "S/L2");
                runner.Contexto.Grafo = g;
                runner.Contexto.InformeCableado = VdfWiringSpec.Validar(g, VdfWiringSpec.Laboratorio());

                string detalle = runner.DetalleDelPasoActual();
                StringAssert.Contains("Sobra un cable", detalle);
                StringAssert.Contains("borne del centro del breaker", detalle, "no enseña el id interno BRK-L2");
                StringAssert.DoesNotContain("BRK-", detalle);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
