using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VDF.Tests
{
    public class VdfFueraDeGuionTests
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

            // Estos tests maltratan el MÓDULO, no una práctica. La escena arranca con la
            // práctica 1, que no deja arrancar el motor (VdfPractica.BloqueaMarcha), así que
            // se quita: el módulo queda libre, como en un banco de pruebas.
            if (_runner != null) _runner.Practica = null;
            _sim.Despertar();
            _sim.PulsarStop();
            yield return null;
        }

        // =====================================================================================
        // Pulsadores en el momento equivocado
        // =====================================================================================

        /// <summary>
        /// Pulsar cualquier cosa sin haber arrancado no debe mover el motor ni reventar.
        /// Es lo primero que hace cualquiera al llegar al tablero: tocarlo todo.
        /// </summary>
        [UnityTest]
        public IEnumerator PulsarloTodoSinArrancar_NoMueveElMotor()
        {
            _sim.PulsarSubir();
            _sim.PulsarBajar();
            _sim.PulsarBajar();
            _sim.PulsarSubir();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 0.5f);

            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "sin START el motor no debería arrancar. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.SalidaS1, "ni mandar la orden de marcha");
        }

        [UnityTest]
        public IEnumerator AporrearSubir_SeQuedaEnLaCuartaYNoMataElMotor()
        {
            _sim.PulsarStart();
            for (int i = 0; i < 10; i++) _sim.PulsarSubir();

            yield return EsperarNivel(PlcFanLogic.NivelMaximo);

            Assert.AreEqual(PlcFanLogic.NivelMaximo, _sim.Plc.NivelActivo,
                "diez pulsaciones deberían dejarlo en la cuarta, no matarlo. " + _sim.Resumen());
            Assert.IsTrue(_sim.Plc.SalidaS1, "y el motor debería seguir en marcha");

            // Y desde ahí todavía se puede bajar, que es lo que lo diferencia de un atasco.
            _sim.PulsarBajar();
            yield return EsperarNivel(PlcFanLogic.NivelMaximo - 1);
            Assert.AreEqual(PlcFanLogic.NivelMaximo - 1, _sim.Plc.NivelActivo);
        }

        /// <summary>Aporrear BAJAR desde parado tampoco debe dejar el nivel en negativo.</summary>
        [UnityTest]
        public IEnumerator AporrearBajar_NoDejaElNivelEnNegativo()
        {
            _sim.PulsarStart();
            for (int i = 0; i < 10; i++) _sim.PulsarBajar();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 0.5f);

            Assert.GreaterOrEqual(_sim.Plc.NivelActivo, 0, "el nivel no puede ser negativo");
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "y debería quedar parado");
        }

        /// <summary>
        /// Cambiar de idea a mitad del retardo: pedir la 2 y arrepentirse volviendo a la 1.
        /// El motor tiene que acabar girando en la 1, no quedarse colgado entre dos niveles.
        /// </summary>
        [UnityTest]
        public IEnumerator ArrepentirseAMitadDelRetardo_AcabaEnElNivelQueSePidioUltimo()
        {
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarSubir();                 // pide la 2
            yield return new WaitForSeconds(1f); // a mitad del retardo
            _sim.PulsarBajar();                 // se arrepiente

            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1f);

            Assert.AreEqual(1, _sim.Plc.NivelActivo,
                "debería acabar en la velocidad 1. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.CambioEnCurso, "y sin ningún cambio colgado");
        }

        /// <summary>
        /// STOP en plena espera: no puede dejar un cambio pendiente que resucite el motor
        /// tres segundos después. Es el peor fallo posible en un panel de seguridad.
        /// </summary>
        [UnityTest]
        public IEnumerator StopDuranteElRetardo_NoResucitaElMotorDespues()
        {
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return new WaitForSeconds(1f);

            _sim.PulsarStop();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1f);

            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "tras STOP el motor no puede arrancar solo al vencer el retardo. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.SalidaS1);
            Assert.IsFalse(_sim.Plc.Habilitado);
        }

        // =====================================================================================
        // Emergencia
        // =====================================================================================

        [UnityTest]
        public IEnumerator ConElHongoPulsado_NadaArranca()
        {
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarEmergencia();
            yield return null;
            Assert.AreEqual(0, _sim.Plc.NivelActivo, "el hongo debería parar el motor en seco");

            // Y ahora, a insistir.
            _sim.PulsarStart();
            _sim.PulsarSubir();
            _sim.PulsarSubir();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1f);

            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "con la emergencia enclavada no debería arrancar. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.SalidaS1);
            Assert.IsFalse(_sim.Plc.Habilitado, "START no debería habilitar con el hongo puesto");
        }

        /// <summary>
        /// Rearmar no arranca el motor solo: deja el sistema en reposo y hay que pulsar
        /// START. Un rearme que relanza el motor es un accidente esperando a ocurrir.
        /// </summary>
        [UnityTest]
        public IEnumerator RearmarDejaEnReposo_NoRelanzaElMotor()
        {
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            _sim.PulsarEmergencia();
            yield return null;
            _sim.Rearmar();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 1f);

            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "rearmar no debería relanzar el motor. " + _sim.Resumen());
            Assert.IsFalse(_sim.Plc.Habilitado, "y hay que volver a pulsar START");

            // Pero después de rearmar, el módulo tiene que volver a funcionar.
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            Assert.AreEqual(1, _sim.Plc.NivelActivo,
                "tras rearmar y pulsar START debería volver a funcionar. " + _sim.Resumen());
        }

        [UnityTest]
        public IEnumerator ElHongoPulsadoDosVeces_SigueEnclavado()
        {
            _sim.PulsarEmergencia();
            _sim.PulsarEmergencia();
            yield return null;

            Assert.IsTrue(_sim.Plc.EmergenciaActiva,
                "pulsarlo dos veces no debería liberarlo: se libera girándolo, no pulsándolo");
        }

        // =====================================================================================
        // Progreso de las prácticas
        // =====================================================================================

        [UnityTest]
        public IEnumerator SubirYLuegoBajar_NoDeshaceElProgresoDeLaPractica4()
        {
            if (_cableado != null) yield return Cablear();

            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;

            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();

            for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
            {
                _sim.PulsarSubir();
                yield return EsperarNivel(n);
            }

            int cumplidosArriba = _runner.PasosCumplidos;
            Assert.Greater(cumplidosArriba, 0, "algo debería haberse cumplido al subir");

            // Ahora baja del todo y para, que es lo que pide el resto de la práctica.
            for (int n = PlcFanLogic.NivelMaximo - 1; n >= 1; n--)
            {
                _sim.PulsarBajar();
                yield return EsperarNivel(n);
            }
            _sim.PulsarStop();
            yield return null;

            Assert.GreaterOrEqual(_runner.PasosCumplidos, cumplidosArriba,
                "bajar y parar no debería hacer RETROCEDER el progreso: los logros se "
                + "recuerdan. Antes " + cumplidosArriba + ", ahora " + _runner.PasosCumplidos);
        }

        /// <summary>
        /// Romper el cableado SÍ debe desmarcar el paso del cableado, porque es una condición
        /// y no un logro: si lo rompes, deja de estar bien. Es el reverso del test anterior,
        /// y los dos juntos son los que fijan la diferencia.
        /// </summary>
        [UnityTest]
        public IEnumerator RomperElCableado_DesmarcaElPasoDelCableado()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _runner.Empezar(VdfPracticasDefaults.CrearCableado());
            yield return Cablear();
            yield return null;

            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto, "el cableado debería estar correcto");
            int conTodo = _runner.PasosCumplidos;

            // Se quita un cable cualquiera de los que importan.
            _cableado.BorrarCablesDe("S1");
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;

            Assert.IsFalse(_sim.Cables.Validar().EsCorrecto,
                "quitando el cable de S1 el cableado ya no puede estar correcto");
            Assert.Less(_runner.PasosCumplidos, conTodo,
                "romper el cableado debería desmarcar su paso: es una condición, no un logro");

            // Y volver a ponerlo lo arregla: el alumno puede rectificar.
            yield return Cablear();
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto, "volver a cablear debería arreglarlo");
            Assert.GreaterOrEqual(_runner.PasosCumplidos, conTodo,
                "y el paso debería volver a marcarse");
        }

        /// <summary>
        /// Cambiar de práctica a mitad y volver no deja nada colgado. Es lo que hace
        /// cualquiera que se aburre: abrir el menú y saltar a otra.
        /// </summary>
        /// <remarks>
        /// Ojo con lo que se mide aquí. La primera versión de este test exigía cero pasos
        /// cumplidos al empezar, y fallaba: la práctica 4 arranca ya configurada y cableada
        /// (<c>EmpezarConfigurado</c> / <c>EmpezarCableado</c>), así que su primer paso
        /// —«comprueba que el variador tiene tensión»— se cumple en el acto. Eso no es
        /// progreso arrastrado, es un paso que ya se satisface, y está bien que sea así.
        ///
        /// Lo que de verdad no puede pasar es que volver a una práctica conserve lo que el
        /// alumno hizo la vez anterior. Eso se mide comparando con el arranque limpio.
        /// </remarks>
        [UnityTest]
        public IEnumerator CambiarDePracticaAMedias_NoDejaNadaColgado()
        {
            // Cuántos pasos se dan por cumplidos nada más empezar, en limpio.
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;
            int alEmpezarEnLimpio = _runner.PasosCumplidos;

            // Se avanza de verdad y se deja a medias, con un cambio de nivel en curso.
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);
            _sim.PulsarSubir();
            yield return new WaitForSeconds(1f); // justo en mitad del retardo

            int aMedias = _runner.PasosCumplidos;
            Assert.Greater(aMedias, alEmpezarEnLimpio, "debería haber avanzado algo");

            // Saltar a otra práctica.
            _runner.Empezar(VdfPracticasDefaults.CrearReconocimiento());
            yield return null;
            Assert.AreEqual(0, _runner.PasosCumplidos,
                "la práctica 1 no arranca con nada cumplido: no se ha señalado nada todavía");
            Assert.Greater(_runner.TotalPasos, 0, "y debería tener pasos");

            // Y volver: tiene que quedar como la primera vez, ni más ni menos.
            _runner.Empezar(VdfPracticasDefaults.CrearOperacion());
            yield return null;
            Assert.AreEqual(alEmpezarEnLimpio, _runner.PasosCumplidos,
                "volver a la práctica 4 debería dejarla como al empezar en limpio, sin "
                + "arrastrar lo que se hizo antes de salir");
            Assert.AreEqual(0, _sim.Plc.NivelActivo,
                "y el módulo debería quedar parado, no con el motor girando de la vez anterior");
        }

        [UnityTest]
        public IEnumerator LasCincoSeguidas_NingunaDejaElModuloInservible()
        {
            foreach (var practica in VdfPracticasDefaults.CrearTodas())
            {
                _runner.Empezar(practica);
                yield return null;

                Assert.Greater(_runner.TotalPasos, 0,
                    "la práctica '" + practica.Titulo + "' no tiene pasos");
                Assert.IsNotNull(_sim.Vfd, "el variador debería seguir existiendo");
                Assert.IsNotNull(_sim.Plc, "y el PLC también");
            }

            // Tras pasar por las cinco, el módulo tiene que seguir siendo operable.
            if (_cableado != null) yield return Cablear();
            _sim.AplicarPuntoDePartida();
            if (_cableado != null) yield return Cablear();

            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return EsperarNivel(1);

            Assert.AreEqual(1, _sim.Plc.NivelActivo,
                "después de recorrer las cinco, el módulo debería seguir arrancando. "
                + _sim.Resumen());
        }

        // =====================================================================================
        // Cableado disparatado
        // =====================================================================================

        /// <summary>
        /// Cablear cualquier cosa con cualquier cosa no debe reventar ni dar por bueno el
        /// cableado. Es lo que hace un alumno que no ha leído el esquema.
        /// </summary>
        [UnityTest]
        public IEnumerator CablearAlAzar_NoDaPorBuenoElCableado()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            // Uniones sin ningún sentido eléctrico, a propósito.
            _cableado.Conectar("S1", "S6");
            _cableado.Conectar("A1", "MP");
            _cableado.Conectar("R+", "MC");
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;

            var informe = _sim.Cables.Validar();
            Assert.IsFalse(informe.EsCorrecto, "un cableado al azar no puede dar correcto");
            Assert.Greater(informe.Sobrantes.Count, 0, "y esas uniones deberían constar como sobrantes");
        }

        /// <summary>
        /// Un borne consigo mismo, o un cable repetido, no deben romper el grafo ni contar
        /// como cableado válido.
        /// </summary>
        [UnityTest]
        public IEnumerator CablesAbsurdos_NoRompenElGrafo()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            _cableado.Conectar("S1", "S1");          // consigo mismo
            _cableado.Conectar("PLC-Q0.7", "S1");
            _cableado.Conectar("PLC-Q0.7", "S1");    // repetido
            _cableado.Conectar("NO_EXISTE", "S1");   // un borne inventado
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;

            // Lo que importa: que nada de esto tire una excepción y que el informe se pueda
            // pedir igual. NUnit falla solo si algo escribió un error en la consola.
            var informe = _sim.Cables.Validar();
            Assert.IsNotNull(informe, "el informe debería existir igualmente");
        }

        /// <summary>
        /// Borrarlo todo a media práctica deja el cableado incorrecto pero recuperable.
        /// El alumno tiene que poder empezar de cero sin reiniciar la aplicación.
        /// </summary>
        [UnityTest]
        public IEnumerator BorrarTodoElCableado_EsRecuperable()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto);

            _cableado.BorrarTodo();
            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;
            Assert.IsFalse(_sim.Cables.Validar().EsCorrecto, "sin cables no puede estar correcto");

            yield return Cablear();
            Assert.IsTrue(_sim.Cables.Validar().EsCorrecto,
                "volver a cablearlo todo debería dejarlo correcto otra vez");
        }

        // =====================================================================================
        // Parámetros disparatados
        // =====================================================================================

        /// <summary>
        /// Escribir valores imposibles en el variador no debe dejarlo en un estado del que
        /// no se pueda salir. Tiene que avisar, y tiene que poder arreglarse.
        /// </summary>
        [UnityTest]
        public IEnumerator ParametrosDisparatados_AvisanYSePuedenArreglar()
        {
            _sim.AplicarPuntoDePartida();
            yield return null;

            _sim.Vfd.EscribirParametro("E1-04", 999f);
            _sim.Vfd.EscribirParametro("E2-01", 0f);
            _sim.Vfd.EscribirParametro("d1-01", -5f);
            yield return null;

            // Da igual si los rechaza o los acepta con aviso: lo que no vale es que los
            // acepte en silencio y el modulo parezca bien configurado.
            bool rechazados = Mathf.Approximately(_sim.Vfd.Valor("d1-01"), -5f) == false;
            bool avisa = _sim.Vfd.ValidarConfiguracion().Count > 0;
            Assert.IsTrue(rechazados || avisa,
                "valores imposibles deberían rechazarse o al menos generar un aviso");

            // Y desde ahí se vuelve a un módulo sano.
            _sim.AplicarPuntoDePartida();
            yield return null;
            Assert.IsTrue(_sim.Vfd.ConfiguracionEsOperable(),
                "el punto de partida debería dejar el variador operable otra vez");
            Assert.IsEmpty(_sim.Vfd.ValidarConfiguracion(),
                "y sin avisos pendientes");
        }

        /// <summary>
        /// El error didáctico de la guía: H1-01 = 1. Con ese valor S1 deja de ser la orden de
        /// marcha, así que el PLC puede mandar lo que quiera y el motor no debería girar.
        ///
        /// Es la falla que da nombre a media práctica 5 y nunca se había comprobado de punta
        /// a punta en la escena.
        /// </summary>
        [UnityTest]
        public IEnumerator ConH101EnUno_ElModuloNoArrancaDesdeLosBornes()
        {
            if (_cableado != null) yield return Cablear();
            _sim.AplicarPuntoDePartida();
            if (_cableado != null) yield return Cablear();
            yield return null;

            _sim.Vfd.EscribirParametro("H1-01", 1f);
            yield return null;

            _sim.PulsarStop();
            yield return null;
            _sim.PulsarStart();
            _sim.PulsarSubir();
            yield return new WaitForSeconds(PlcFanLogic.RetardoCambioNivelSegundos + 2f);

            // El PLC hace su parte: manda la orden por S1.
            Assert.IsTrue(_sim.Plc.SalidaS1,
                "el PLC debería seguir mandando la orden por S1: el fallo está en el variador");

            // Pero el variador no la entiende como marcha, así que no debería girar.
            Assert.AreEqual(0f, _sim.Vfd.FrecuenciaSalidaHz, 0.01f,
                "con H1-01 = 1 el borne S1 es LOCAL/REMOTO, no marcha: el motor no debería "
                + "girar. " + _sim.Resumen());

            // Y corrigiendo el parámetro vuelve a funcionar, que es la lección.
            _sim.Vfd.EscribirParametro("H1-01", 40f);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(_sim.Vfd.FrecuenciaSalidaHz, 0f,
                "al poner H1-01 = 40 el motor debería arrancar. " + _sim.Resumen());
        }

        [UnityTest]
        public IEnumerator LosCables_SeApoyanEnLaMesaYNoLaAtraviesan()
        {
            if (_cableado == null) Assert.Ignore("la escena no tiene cableado por ratón");

            yield return Cablear();
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(VdfCablePhysics.Colisionadores.Count, 0,
                "no hay ni un colisionador registrado: los cables caerian sin chocar con nada");

            var cables = Object.FindObjectsByType<VdfCablePhysics>(FindObjectsInactive.Include);
            Assert.Greater(cables.Length, 0, "no se tendió ningún cable");

            float encimera = VdfReconstructorEscena.AlturaEncimera;
            float margen = 0.010f;   // holgura generosa sobre el grosor del cable

            int hundidos = 0, sinAsentar = 0, mirados = 0;
            float peor = 0f;
            string culpable = "";

            foreach (var c in cables)
            {
                // Un cable sin asentar tiene todos sus nodos en el origen y daria un falso
                // positivo de 75 cm «dentro» de la mesa. Se cuentan aparte.
                if (c.LongitudActual() < 0.001f) { sinAsentar++; continue; }
                mirados++;

                for (int i = 0; i < c.Nodos; i++)
                {
                    float dentro = encimera - c.PosicionNodo(i).y;
                    if (dentro <= margen) continue;

                    hundidos++;
                    if (dentro > peor)
                    {
                        peor = dentro;
                        culpable = c.name + " nodo " + i;
                    }
                }
            }

            // Los que no se asentaron son restos: BorrarTodo marca para destruir y Unity lo
            // hace al final del fotograma, asi que FindObjectsByType los sigue devolviendo
            // un rato con los nodos a cero. Se ignoran.
            Assert.Greater(mirados, 0,
                "no se pudo mirar ningún cable asentado (" + sinAsentar + " sin asentar de "
                + cables.Length + ")");

            Assert.AreEqual(0, hundidos,
                hundidos + " nodos de cable por debajo de la encimera, sobre " + mirados
                + " cables. El peor, " + (peor * 1000f).ToString("0") + " mm dentro ("
                + culpable + "). Lo habitual es que los colisionadores no esten registrados.");
        }

        // =====================================================================================
        // Herramientas
        // =====================================================================================

        private IEnumerator Cablear()
        {
            if (_cableado == null) yield break;

            _cableado.IndexarBornes();
            _cableado.BorrarTodo();

            foreach (var red in VdfWiringSpec.Practica())
                for (int i = 0; i + 1 < red.Bornes.Length; i++)
                    _cableado.Conectar(red.Bornes[i], red.Bornes[i + 1]);

            _cableado.AvisarCableadoCambiado();
            _sim.RevisarCableado();
            yield return null;
        }

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
    }
}
