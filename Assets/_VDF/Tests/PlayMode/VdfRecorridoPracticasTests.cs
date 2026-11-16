using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    public class VdfRecorridoPracticasTests
    {
        private const string Escena = "VDF_Desktop";

        private VdfSimulacion _sim;
        private VdfPracticaRunner _runner;
        private VdfJumperController _cableado;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores; // estos tests recorren la logica de pulsadores
            SceneManager.LoadScene(Escena, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            _cableado = Object.FindAnyObjectByType<VdfJumperController>();

            Assert.IsNotNull(_sim, "la escena no tiene VdfSimulacion");
            Assert.IsNotNull(_runner, "la escena no tiene VdfPracticaRunner");

            _sim.Despertar();
            yield return null;
        }

        // =====================================================================================
        // Práctica 1: reconocimiento
        // =====================================================================================

        /// <summary>
        /// Señalar cada componente que pide el guion hasta terminar la práctica.
        ///
        /// Lo que esto comprueba y EditMode no: que **cada id que pide el guion existe de
        /// verdad en el tablero construido**. Si alguien renombra un borne en el layout y se
        /// olvida del guion, el paso no se cumpliría nunca y el alumno se quedaría atascado
        /// sin entender por qué.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica1_SeCompletaIdentificandoLoQuePide()
        {
            _runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            yield return null;

            Assert.Greater(_runner.TotalPasos, 0, "la práctica 1 no tiene pasos");

            yield return RecorrerIdentificando(_runner);

            Assert.IsTrue(_runner.Completada,
                "la práctica 1 no se pudo completar. " + Atasco(_runner));
        }

        /// <summary>
        /// Todo id que la práctica 1 pide señalar tiene que existir en el tablero.
        ///
        /// Se comprueba aparte del recorrido para que, si falla, el mensaje diga exactamente
        /// qué id falta en vez de un «no se completó» genérico.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica1_TodoLoQuePideExisteEnElTablero()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");
            _cableado.IndexarBornes();

            var practica = VdfPracticasDefaults.CrearReconocimiento();
            var faltan = new List<string>();

            for (int i = 0; i < practica.TotalPasos; i++)
            {
                var paso = practica.Paso(i);
                if (paso.Condicion != VdfCondicion.ComponenteIdentificado) continue;
                if (string.IsNullOrEmpty(paso.Objetivo)) continue;

                // Puede ser un borne o cualquier otra pieza marcada del tablero.
                if (_cableado.BuscarBorne(paso.Objetivo) == null && !ExistePiezaMarcada(paso.Objetivo))
                    faltan.Add(paso.Objetivo);
            }

            Assert.IsEmpty(faltan,
                "la práctica 1 pide señalar piezas que no están en el tablero: "
                + string.Join(", ", faltan.ToArray()));
            yield return null;
        }

        // =====================================================================================
        // Práctica 2: configuración del variador
        // =====================================================================================

        /// <summary>
        /// Programar los parámetros que pide el guion y llegar al final.
        ///
        /// Se escriben los valores por el modelo, no tecleando, porque el teclado ya tiene
        /// sus 24 tests. Lo que se mide aquí es que **los valores que pide el guion dejan al
        /// variador operable y sin avisos**, que son los dos últimos pasos de la práctica.
        /// Si un valor del guion fuera incompatible con la validación del variador, la
        /// práctica sería imposible de terminar y nadie se enteraría.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica2_SeCompletaProgramandoLosParametros()
        {
            _runner.Empezar(VdfPracticasDefaults.CrearConfiguracion());
            yield return null;

            // Primero se inicializa, como pide el paso 1: después de eso se programa.
            _sim.Vfd.EscribirParametro("A1-03", 2220f);
            yield return null;

            var practica = _runner.Practica;
            for (int i = 0; i < practica.TotalPasos; i++)
            {
                var paso = practica.Paso(i);
                if ((paso.Condicion == VdfCondicion.ParametroEnValor ||
                     paso.Condicion == VdfCondicion.ParametroRevisado) &&
                    !string.IsNullOrEmpty(paso.Objetivo))
                {
                    _sim.Vfd.EscribirParametro(paso.Objetivo, paso.Valor);
                }
            }
            yield return null;

            yield return RecorrerIdentificando(_runner);

            Assert.IsTrue(_sim.Vfd.ConfiguracionEsOperable(),
                "con los valores del guion el variador debería quedar operable");
            Assert.IsEmpty(_sim.Vfd.ValidarConfiguracion(),
                "con los valores del guion no debería quedar ningún aviso");
            Assert.IsTrue(_runner.Completada,
                "la práctica 2 no se pudo completar. " + Atasco(_runner));
        }

        // =====================================================================================
        // Práctica 3: cableado
        // =====================================================================================

        /// <summary>
        /// Cablear el módulo entero siguiendo la especificación y llegar al final.
        ///
        /// Este es el recorrido que más cosas puede romper, porque toca el tablero: cada
        /// unión nombra dos bornes que tienen que existir. Es también donde se vería que el
        /// runner no está escuchando al cableado.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica3_SeCompletaCableandoElModulo()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _runner.Empezar(VdfPracticasDefaults.CrearCableado());
            yield return null;

            yield return Cablear();

            var informe = _sim.Cables.Validar();
            Assert.IsTrue(informe.EsCorrecto,
                "el cableado de VdfWiringSpec debería dar correcto. Faltan: "
                + informe.Faltantes.Count + ", sobran: " + informe.Sobrantes.Count);
            Assert.IsEmpty(informe.Sobrantes, "no debería sobrar ningún cable");

            yield return RecorrerIdentificando(_runner);

            Assert.IsTrue(_runner.Completada,
                "la práctica 3 no se pudo completar. " + Atasco(_runner));
        }

        /// <summary>
        /// Todo borne que nombra la especificación de cableado existe en la escena.
        ///
        /// Es el test que habría cazado en el acto cualquier renombrado de borne. Falla con
        /// la lista exacta de los que no están.
        /// </summary>
        [UnityTest]
        public IEnumerator ElCableadoEsperado_SoloNombraBornesQueExisten()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");
            _cableado.IndexarBornes();

            var faltan = new List<string>();
            foreach (var red in VdfWiringSpec.Practica())
                foreach (string id in red.Bornes)
                    if (_cableado.BuscarBorne(id) == null && !faltan.Contains(id))
                        faltan.Add(id);

            Assert.IsEmpty(faltan,
                "VdfWiringSpec nombra bornes que no existen en el tablero: "
                + string.Join(", ", faltan.ToArray()));
            yield return null;
        }

        // =====================================================================================
        // Práctica 4: operación
        // =====================================================================================

        /// <summary>
        /// Operar el módulo de verdad: arrancar, subir a la cuarta, bajar a la primera y parar.
        ///
        /// Aquí el reloj corre, así que entre nivel y nivel hay que esperar el retardo real.
        /// Es el único sitio donde se comprueba que el <see cref="PlcFanLogic"/> de la escena
        /// está recibiendo <c>Tick</c> de alguien: en EditMode el tiempo se avanza a mano.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica4_SeCompletaOperandoElModulo()
        {
            if (_cableado != null) yield return Cablear();

            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;

            _sim.PulsarStop();
            yield return null;

            _sim.PulsarStart();
            Assert.IsTrue(_sim.Plc.Habilitado, "START debería habilitar el PLC");

            for (int nivel = 1; nivel <= PlcFanLogic.NivelMaximo; nivel++)
            {
                _sim.PulsarSubir();
                yield return EsperarNivel(nivel);
                Assert.AreEqual(nivel, _sim.Plc.NivelActivo,
                    "no se alcanzó la velocidad " + nivel + ". " + _sim.Resumen());
            }

            for (int nivel = PlcFanLogic.NivelMaximo - 1; nivel >= 1; nivel--)
            {
                _sim.PulsarBajar();
                yield return EsperarNivel(nivel);
                Assert.AreEqual(nivel, _sim.Plc.NivelActivo,
                    "no se bajó a la velocidad " + nivel + ". " + _sim.Resumen());
            }

            _sim.PulsarStop();
            yield return null;
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "tras STOP debería quedar parado");

            yield return RecorrerIdentificando(_runner);

            Assert.IsTrue(_runner.Completada,
                "la práctica 4 no se pudo completar. " + Atasco(_runner));
        }

        [UnityTest]
        public IEnumerator Practica4_ConInterruptores_SeCompletaYLaCajaSeVe()
        {
            var caja = BuscarRaizIncluidaInactiva(VdfSimulacion.NombreInterruptores);
            Assert.IsNotNull(caja, "la escena no trae la caja de interruptores: reconstruirla");
            Assert.IsFalse(caja.activeSelf, "con pulsadores, la caja no se ve");

            _sim.CambiarModoMando(VdfModoMando.Interruptores);
            Assert.IsTrue(caja.activeSelf, "con interruptores, la caja aparece");

            if (_cableado != null) yield return Cablear();

            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;
            StringAssert.Contains("interruptor S4", _runner.Practica.Pasos[1].Instruccion, "debería usar el guion de interruptores");

            _sim.FijarInterruptor(VdfInterruptor.Marcha, true);
            Assert.IsTrue(_sim.Plc.Habilitado, "MARCHA arriba debería habilitar el PLC");

            bool[,] combinaciones = { { false, false }, { true, false }, { false, true }, { true, true } };
            for (int nivel = 1; nivel <= PlcFanLogic.NivelMaximo; nivel++)
            {
                _sim.FijarInterruptor(VdfInterruptor.S5, combinaciones[nivel - 1, 0]);
                _sim.FijarInterruptor(VdfInterruptor.S6, combinaciones[nivel - 1, 1]);
                yield return EsperarNivel(nivel);
                Assert.AreEqual(nivel, _sim.Plc.NivelActivo, "no se alcanzó la velocidad " + nivel + ". " + _sim.Resumen());
            }

            _sim.FijarInterruptor(VdfInterruptor.S5, false);
            _sim.FijarInterruptor(VdfInterruptor.S6, false);
            yield return EsperarNivel(1);

            _sim.FijarInterruptor(VdfInterruptor.Marcha, false);
            yield return null;
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "MARCHA abajo debería parar");

            yield return RecorrerIdentificando(_runner);
            Assert.IsTrue(_runner.Completada, "la práctica 4 con interruptores no se pudo completar. " + Atasco(_runner));
        }

        [UnityTest]
        public IEnumerator EnEscritorio_CadaVistaEncuadraSuPieza()
        {
            var cam = Camera.main;
            Assert.IsNotNull(cam);
            var orbita = cam.GetComponent<VdfCamaraOrbital>();
            Assert.IsNotNull(orbita, "la cámara de escritorio no tiene órbita");
            yield return null;

            var piezas = new[]
            {
                VdfPanelBuilder.NombreRaiz,      // F1, el conjunto: al menos el tablero
                "AnclajeTeclado",                // F2
                VdfPanelBuilder.NombreRaiz,      // F3, la regleta está en el tablero
                VdfLadderPanel.NombreRaiz        // F4
            };
            for (int i = 0; i < piezas.Length; i++)
            {
                orbita.IrAYa(i);
                var go = GameObject.Find(piezas[i]);
                Assert.IsNotNull(go, piezas[i]);
                Vector3 p = cam.WorldToViewportPoint(go.transform.position);
                if (piezas[i] == VdfLadderPanel.NombreRaiz || piezas[i] == VdfPanelBuilder.NombreRaiz)
                {
                    var b = new Bounds(go.transform.position, Vector3.zero);
                    bool hay = false;
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
                    p = cam.WorldToViewportPoint(b.center);
                }
                Assert.Greater(p.z, 0f, "vista " + (i + 1) + ": " + piezas[i] + " queda detrás de la cámara");
                Assert.That(p.x, Is.InRange(0.15f, 0.85f), "vista " + (i + 1) + ": " + piezas[i] + " fuera del cuadro");
                Assert.That(p.y, Is.InRange(0.15f, 0.85f), "vista " + (i + 1) + ": " + piezas[i] + " fuera del cuadro");
            }
        }

        [UnityTest]
        public IEnumerator LaEscena_SaleConLaPracticaDelLaboratorio()
        {
            VdfSimulacion.ModoMandoForzado = null;
            SceneManager.LoadScene(Escena, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);

            _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            _cableado = Object.FindAnyObjectByType<VdfJumperController>();
            Assert.AreEqual(VdfModoMando.Interruptores, _sim.ModoMando, "reconstruir las escenas");
            Assert.AreEqual(VdfModoMando.Interruptores, VdfMando.Activo);

            var caja = BuscarRaizIncluidaInactiva(VdfSimulacion.NombreInterruptores);
            Assert.IsNotNull(caja);
            Assert.IsTrue(caja.activeSelf, "con interruptores, la caja se ve");

            // La escena de escritorio no cablea nada hasta que se elige una práctica: se
            // empieza la 4, que arranca cableada y configurada, como haría el alumno.
            _runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;
            yield return null;
            StringAssert.Contains("interruptor S4", _runner.Practica.Pasos[1].Instruccion, "guion de interruptores");

            Assert.IsTrue(_sim.InformeCableado != null && _sim.InformeCableado.EsCorrecto,
                "arranca cableado como en el laboratorio. " + _sim.InformeCableado);
            Assert.IsTrue(_sim.Cables.Grafo.MismaRed("PLC-Q0.5", "S4"));
            Assert.AreEqual(40, _sim.Vfd.ValorEntero("H1-04"));
            Assert.AreEqual(14, _sim.Vfd.ValorEntero("H1-01"));

            _sim.FijarInterruptor(VdfInterruptor.Marcha, true);
            yield return EsperarNivel(1);
            float t = 0f;
            while (_sim.Vfd.FrecuenciaSalidaHz < 9.9f && t < 8f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(10f, _sim.Vfd.FrecuenciaSalidaHz, 0.1f, _sim.Resumen());

            _sim.FijarInterruptor(VdfInterruptor.S5, true);
            float minima = float.MaxValue;
            t = 0f;
            while (_sim.Plc.NivelActivo != 2 && t < 6f)
            {
                minima = Mathf.Min(minima, _sim.Vfd.FrecuenciaSalidaHz);
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(2, _sim.Plc.NivelActivo, "el piloto V2 llega tras el retardo");
            Assert.GreaterOrEqual(minima, 9.9f, "con el Ladder del laboratorio el motor no se para entre velocidades");

            _sim.FijarInterruptor(VdfInterruptor.Marcha, false);
            VdfSimulacion.ModoMandoForzado = VdfModoMando.Pulsadores;
        }

        private static GameObject BuscarRaizIncluidaInactiva(string nombre)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.parent == null && t.name == nombre) return t.gameObject;
            return null;
        }

        /// <summary>
        /// Lo que el Ladder original hace y el simulador debe reflejar: al cambiar de nivel
        /// el motor cae en el acto y vuelve tras el retardo. Se comprueba en la escena, con
        /// el reloj de verdad, no avanzando el tiempo a mano.
        /// </summary>
        [UnityTest]
        public IEnumerator EnLaEscena_ElMotorSeParaAlCambiarDeNivel()
        {
            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            Assert.AreEqual(1, _sim.Plc.NivelActivo);

            _sim.PulsarSubir();
            yield return null;

            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "al pedir la velocidad 2 el motor debería caer en el acto");
            Assert.IsFalse(_sim.Plc.SalidaS1, "y la orden de marcha con él");

            yield return EsperarNivel(2);
            Assert.AreEqual(2, _sim.Plc.NivelActivo, "y arrancar ya en la velocidad 2");
        }

        // =====================================================================================
        // Práctica 5: seguridad y fallas
        // =====================================================================================

        /// <summary>
        /// La práctica 5 con una avería concreta forzada: accionar la emergencia, rearmar,
        /// diagnosticar la avería y repararla.
        ///
        /// Se fuerza la avería en vez de dejar que la elija al azar, porque un test que
        /// depende del azar no sirve para diagnosticar nada cuando falla.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica5_SeCompletaConUnaAveriaForzada()
        {
            if (_cableado != null) yield return Cablear();

            var averias = VdfCatalogoFallas.Crear();
            Assert.Greater(averias.Count, 0, "el catálogo de averías está vacío");

            _runner.FallaForzada = averias[0].Id;
            _runner.Empezar(VdfPracticasDefaults.CrearSeguridadYFallas());
            yield return null;

            // Los dos primeros pasos son de lectura: el alumno los confirma.
            _runner.ConfirmarPasoManual();
            _runner.ConfirmarPasoManual();
            yield return null;

            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            float t = 0f;
            while (_sim.Vfd.FrecuenciaSalidaHz < 2f && t < 5f) { t += Time.deltaTime; yield return null; }

            _sim.PulsarEmergencia();
            yield return null;
            Assert.IsTrue(_sim.Plc.EmergenciaActiva, "el hongo debería quedar enclavado");
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "y el motor parado");

            Assert.IsTrue(_runner.ConfirmarPasoManual(), "el paso de observar la falla no admitió LISTO");
            yield return null;

            _sim.Rearmar();
            yield return null;
            Assert.IsFalse(_sim.Plc.EmergenciaActiva, "rearmar debería liberar el hongo");

            // La avería llega con el paso «a partir de aquí...», no antes.
            for (int f = 0; f < 5; f++) yield return null;

            // Y ahora lo que de verdad pide la practica: reparar la averia. Reparar es
            // deshacer lo que hizo Aplicar, que es justo lo que hace el alumno con el
            // teclado o volviendo a poner el cable.
            var activa = _runner.Contexto.FallaActiva;
            Assert.IsNotNull(activa, "la practica 5 deberia haber provocado una averia");
            yield return Reparar(activa);

            Assert.IsTrue(activa.Reparada(_sim.Vfd, _sim.Cables != null ? _sim.Cables.Grafo : null),
                "la averia '" + activa.Id + "' deberia quedar reparada");

            // Y se comprueban las cuatro velocidades, que es lo que pide el paso.
            _sim.PulsarStart();
            for (int nivel = 1; nivel <= PlcFanLogic.NivelMaximo; nivel++)
            {
                _sim.PulsarSubir();
                yield return EsperarNivel(nivel);
            }
            yield return null;

            yield return RecorrerIdentificando(_runner);

            Assert.IsTrue(_runner.Completada,
                "la práctica 5 no se pudo completar. " + Atasco(_runner));
        }

        /// <summary>
        /// Toda avería del catálogo tiene que poder repararse. Una avería sin arreglo deja
        /// la práctica 5 bloqueada para siempre, y es el tipo de cosa que solo se ve
        /// probándolas una por una.
        /// </summary>
        [UnityTest]
        public IEnumerator TodaAveria_SeNotaYSePuedeReparar()
        {
            if (_cableado != null) yield return Cablear();

            var problemas = new List<string>();

            foreach (var averia in VdfCatalogoFallas.Crear())
            {
                // Punto de partida sano: el modulo bien configurado y bien cableado.
                _sim.AplicarPuntoDePartida();
                if (_cableado != null) yield return Cablear();
                yield return null;

                var grafo = _sim.Cables != null ? _sim.Cables.Grafo : null;

                if (!averia.Reparada(_sim.Vfd, grafo))
                {
                    problemas.Add(averia.Id + ": con el modulo sano ya consta averiada, "
                                  + "asi que el alumno empezaria con la averia puesta");
                    continue;
                }

                // Provocarla tiene que notarse.
                averia.Aplicar(_sim.Vfd, grafo);
                if (_cableado != null) _cableado.AvisarCableadoCambiado();
                _sim.RevisarCableado();
                yield return null;

                grafo = _sim.Cables != null ? _sim.Cables.Grafo : null;

                if (!averia.EstaPresente(_sim.Vfd, grafo))
                    problemas.Add(averia.Id + ": aplicarla no deja rastro, EstaPresente dice que no");

                if (averia.Reparada(_sim.Vfd, grafo))
                    problemas.Add(averia.Id + ": aplicarla no cambia nada, sigue dando por reparada");
            }

            Assert.IsEmpty(problemas,
                "averías con problemas:\n  " + string.Join("\n  ", problemas.ToArray()));
        }

        /// <summary>
        /// El panel de diagnostico tiene que ofrecer un boton por cada averia del catalogo.
        ///
        /// Si el catalogo crece y los botones no, las ultimas averias no se pueden señalar y
        /// la practica 5 queda imposible de terminar justo cuando sale una de ellas, que es
        /// la clase de fallo que aparece una vez de cada cinco y vuelve loco a cualquiera.
        /// </summary>
        [UnityTest]
        public IEnumerator ElPanelDeDiagnostico_OfreceTodasLasAverias()
        {
            var panel = Object.FindAnyObjectByType<VdfPanelDiagnostico>();
            if (panel == null) Assert.Ignore("la escena no tiene panel de diagnostico");

            int enElCatalogo = VdfCatalogoFallas.Crear().Count;
            int botones = panel.GetComponentsInChildren<VdfBotonPractica>(true).Length;

            Assert.GreaterOrEqual(botones, enElCatalogo,
                "el catalogo tiene " + enElCatalogo + " averias y el panel solo " + botones
                + " botones: las ultimas no se podrian señalar nunca");
            yield return null;
        }

        /// <summary>
        /// Acertar la averia en el panel marca el paso de diagnostico, y fallar no lo marca.
        /// Es el camino que recorre el alumno de verdad, con clic incluido.
        /// </summary>
        [UnityTest]
        public IEnumerator Practica5_AcertarEnElPanel_MarcaElDiagnostico()
        {
            var panel = Object.FindAnyObjectByType<VdfPanelDiagnostico>();
            if (panel == null) Assert.Ignore("la escena no tiene panel de diagnostico");

            var averias = VdfCatalogoFallas.Crear();
            _runner.FallaForzada = averias[0].Id;
            _runner.Empezar(VdfPracticasDefaults.CrearSeguridadYFallas());
            yield return null;
            _runner.ProvocarAveriaAhora();   // sin recorrer la parte de seguridad

            Assert.IsNotNull(_runner.Contexto.FallaActiva, "deberia haber una averia activa");

            // Primero una equivocada: no debe darla por buena.
            if (averias.Count > 1)
            {
                int otra = averias[1].Id == _runner.Contexto.FallaActiva.Id ? 2 : 1;
                if (otra < averias.Count)
                {
                    Assert.IsFalse(panel.Senalar(otra), "una averia equivocada no deberia acertar");
                    Assert.IsFalse(panel.Acertado);
                }
            }

            // Ahora la correcta.
            int indice = averias.FindIndex(delegate(VdfFallaDidactica a)
            {
                return a.Id == _runner.Contexto.FallaActiva.Id;
            });
            Assert.GreaterOrEqual(indice, 0, "la averia activa no esta en el catalogo");

            Assert.IsTrue(panel.Senalar(indice), "señalar la averia correcta deberia acertar");
            yield return null;

            Assert.IsTrue(_runner.Contexto.Identificados.Contains(_runner.Contexto.FallaActiva.Id),
                "acertar en el panel deberia registrar el diagnostico en la practica");
        }

        // =====================================================================================
        // Herramientas
        // =====================================================================================

        /// <summary>
        /// Avanza la práctica marcando lo que pide, hasta que se complete o se atasque.
        ///
        /// Un paso automático (cableado, nivel alcanzado…) ya debería estar cumplido cuando
        /// se llega a él, porque el recorrido lo ha provocado antes. Los manuales y los de
        /// identificar se marcan aquí, que es lo que hace el alumno al leerlos o señalar.
        ///
        /// El límite de vueltas no es decorativo: si un paso no se cumple nunca, sin él el
        /// test se colgaría en vez de fallar.
        /// </summary>
        private static IEnumerator RecorrerIdentificando(VdfPracticaRunner runner)
        {
            int vueltas = 0;
            int limite = runner.TotalPasos * 3 + 10;

            while (!runner.Completada && vueltas++ < limite)
            {
                int i = runner.PasoActual();
                if (i < 0) break;

                var paso = runner.Practica.Paso(i);

                if (paso.Condicion == VdfCondicion.Manual)
                    runner.ConfirmarPasoManual();
                else if (paso.Condicion == VdfCondicion.ComponenteIdentificado)
                    runner.Identificar(paso.Objetivo);
                else if (paso.Condicion == VdfCondicion.FallaDiagnosticada)
                {
                    // El paso de diagnostico no lleva objetivo en el guion, y es correcto:
                    // la averia se sortea al empezar, asi que el id acertado solo se conoce
                    // en tiempo de ejecucion. El alumno la elige en el panel de diagnostico;
                    // aqui se acierta a la primera, que es lo que interesa medir.
                    if (runner.Contexto != null && runner.Contexto.FallaActiva != null)
                        runner.Identificar(runner.Contexto.FallaActiva.Id);
                    else
                        break; // sin averia activa no hay nada que diagnosticar
                }
                else if (paso.Condicion == VdfCondicion.GrupoIdentificado)
                    IdentificarGrupo(runner, paso.Objetivo);
                else if (paso.Condicion == VdfCondicion.ParametroRevisado)
                {
                    // «Entra a ver X»: lo que haría el alumno en el teclado.
                    var teclado = runner.Simulacion != null && runner.Simulacion.Operador != null
                        ? runner.Simulacion.Operador.Teclado : null;
                    if (teclado == null || !teclado.IrAParametro(paso.Objetivo)) break;
                    runner.Simulacion.Operador.Pulsar(VfdKey.Enter);
                    runner.Simulacion.Operador.Pulsar(VfdKey.Esc);
                }
                else
                    break; // automático y sin cumplir: el recorrido se ha quedado corto

                yield return null;
            }
        }

        private static void IdentificarGrupo(VdfPracticaRunner runner, string grupo)
        {
            List<string> miembros;
            if (runner.Contexto == null || grupo == null) return;
            if (!runner.Contexto.Grupos.TryGetValue(grupo, out miembros)) return;
            foreach (string id in miembros) runner.Identificar(id);
        }

        /// <summary>
        /// Repara una averia, que es deshacer exactamente lo que hizo Aplicar: devolver el
        /// parametro a su valor correcto, descruzar los dos que se intercambiaron, o volver
        /// a poner el cable que se quito. Es lo que hace el alumno con el teclado del
        /// variador o enchufando otra vez.
        /// </summary>
        private IEnumerator Reparar(VdfFallaDidactica averia)
        {
            switch (averia.Tipo)
            {
                case VdfTipoFalla.ParametroMal:
                    _sim.Vfd.EscribirParametro(averia.ObjetivoA, averia.ValorCorrectoA);
                    break;

                case VdfTipoFalla.ParametrosIntercambiados:
                    _sim.Vfd.EscribirParametro(averia.ObjetivoA, averia.ValorCorrectoA);
                    _sim.Vfd.EscribirParametro(averia.ObjetivoB, averia.ValorCorrectoB);
                    break;

                case VdfTipoFalla.CableFaltante:
                    if (_cableado != null)
                    {
                        _cableado.Conectar(averia.ObjetivoA, averia.ObjetivoB);
                        _cableado.AvisarCableadoCambiado();
                        _sim.RevisarCableado();
                    }
                    break;
            }
            yield return null;
        }

        /// <summary>Deja el módulo cableado según la especificación.</summary>
        private IEnumerator Cablear()
        {
            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            foreach (var red in VdfWiringSpec.Practica())
                for (int i = 0; i + 1 < red.Bornes.Length; i++)
                    _cableado.Conectar(red.Bornes[i], red.Bornes[i + 1]);

            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;
        }

        /// <summary>Espera a que el PLC aplique el nivel pedido, con margen sobre el retardo.</summary>
        private IEnumerator EsperarNivel(int nivel)
        {
            float limite = PlcFanLogic.RetardoCambioNivelSegundos + 2f;
            float t = 0f;
            while (_sim.Plc.NivelActivo != nivel && t < limite)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Dice en qué paso se quedó atascada la práctica y por qué condición.</summary>
        private static string Atasco(VdfPracticaRunner runner)
        {
            int i = runner.PasoActual();
            if (i < 0) return "(no hay paso pendiente)";
            var paso = runner.Practica.Paso(i);
            return "Atascada en el paso " + (i + 1) + " de " + runner.TotalPasos
                 + " [" + paso.Condicion + " objetivo='" + paso.Objetivo + "']: "
                 + paso.Instruccion;
        }

        /// <summary>
        /// ¿Existe en la escena algo que se pueda señalar con ese id?
        ///
        /// Vale un borne del tablero o un objeto cuyo nombre lo lleve: el constructor
        /// nombra las piezas con su id (Pulsador_START, PilotoLed_FUNC...), asi que esa
        /// es la via para las que no son bornes.
        /// </summary>
        private bool ExistePiezaMarcada(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_cableado != null && _cableado.BuscarBorne(id) != null) return true;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == id || t.name.EndsWith("_" + id)) return true;

            return false;
        }
        [UnityTest]
        public IEnumerator UnPasoManual_SePuedePasarPulsandoElBotonDeLaEscena()
        {
            _runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            yield return null;

            var panel = Object.FindAnyObjectByType<VdfPanelGuia>();
            Assert.IsNotNull(panel, "la escena no tiene panel de guia");
            panel.Refrescar();
            yield return null;

            var paso = _runner.Practica.Paso(_runner.PasoActual());
            Assert.AreEqual(VdfCondicion.Manual, paso.Condicion,
                "este test asume que el primer paso de la practica 1 se confirma a mano");

            VdfBotonPractica boton = null;
            foreach (var b in Object.FindObjectsByType<VdfBotonPractica>(FindObjectsInactive.Include))
                if (b.Indice == VdfPanelGuia.IndiceBotonConfirmar) boton = b;

            Assert.IsNotNull(boton,
                "no hay boton de confirmar en la escena: los pasos manuales serian imposibles");
            Assert.IsTrue(boton.gameObject.activeSelf,
                "el boton tiene que estar VISIBLE cuando el paso actual se confirma a mano");

            int antes = _runner.PasoActual();
            boton.Pulsar();
            yield return null;

            Assert.AreNotEqual(antes, _runner.PasoActual(),
                "pulsar el boton tiene que pasar de paso. Si no, el alumno se queda atascado");

            panel.Refrescar();
            yield return null;
            Assert.IsFalse(boton.gameObject.activeSelf,
                "y el boton se esconde cuando el paso siguiente no se confirma a mano");
        }

    }
}
