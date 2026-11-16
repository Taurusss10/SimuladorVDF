using System.Collections.Generic;
using NUnit.Framework;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 6: el diagrama Ladder equivalente.
    ///
    /// El riesgo de esta etapa no es que el diagrama se dibuje mal, es que **diga algo
    /// distinto de lo que hace el PLC**. Un diagrama es una segunda descripción de la misma
    /// lógica, y dos descripciones de lo mismo se separan en cuanto alguien toca una. Si el
    /// alumno ve un contacto cerrado y el motor no arranca, el simulador le está enseñando
    /// algo falso, que es peor que no enseñarle nada.
    ///
    /// Por eso el test central recorre la secuencia completa del ventilador comparando, en
    /// cada instante, lo que dice el diagrama con lo que dice <see cref="PlcFanLogic"/>.
    /// </summary>
    public class VdfLadderTests
    {
        private List<VdfLadderPeldano> _peldanos;
        private PlcFanLogic _plc;
        private VdfEstadoLadder _estado;

        [SetUp]
        public void SetUp()
        {
            _peldanos = VdfLadderPrograma.Crear();
            _plc = new PlcFanLogic();
            _estado = new VdfEstadoLadder(_plc);
        }

        private void Avanzar(float segundos)
        {
            int n = UnityEngine.Mathf.RoundToInt(segundos / 0.1f);
            for (int i = 0; i < n; i++)
            {
                _plc.Tick(0.1f);
                _estado.Tick(0.1f);
                ComprobarCoherencia("durante el avance");
            }
        }

        private void ComprobarCoherencia(string cuando)
        {
            var fallos = VdfLadderPrograma.Contrastar(_peldanos, _estado);
            Assert.AreEqual(0, fallos.Count,
                "El diagrama y el PLC no dicen lo mismo " + cuando + ":\n  " +
                string.Join("\n  ", fallos.ToArray()));
        }

        // =====================================================================================
        // El test que importa
        // =====================================================================================

        [Test]
        public void ElDiagramaYElPlc_DicenLoMismoEnTodaLaSecuencia()
        {
            ComprobarCoherencia("en reposo");

            _plc.PulsarStart(); _estado.AvisarStart();
            ComprobarCoherencia("justo tras START");
            Avanzar(1f);

            for (int nivel = 1; nivel <= PlcFanLogic.NivelMaximo; nivel++)
            {
                _plc.PulsarSubir(); _estado.AvisarSubir();
                ComprobarCoherencia("justo tras pulsar subir");
                Avanzar(4f);
                Assert.AreEqual(nivel, _plc.NivelActivo);
            }

            for (int i = 0; i < 3; i++)
            {
                _plc.PulsarBajar(); _estado.AvisarBajar();
                Avanzar(4f);
            }

            _plc.PulsarStop(); _estado.AvisarStop();
            ComprobarCoherencia("justo tras STOP");
            Avanzar(1f);

            _plc.PulsarEmergencia();
            ComprobarCoherencia("con la seta pulsada");
            Avanzar(1f);

            _plc.RearmarEmergencia();
            ComprobarCoherencia("tras rearmar");
            Avanzar(1f);
        }

        // =====================================================================================
        // Peldaños concretos
        // =====================================================================================

        private VdfLadderPeldano PorBobina(string etiqueta)
        {
            foreach (var p in _peldanos)
                if (p.Bobina != null && p.Bobina.Etiqueta == etiqueta) return p;
            return null;
        }

        [Test]
        public void ElEnclavamiento_SeMantieneCuandoSeSueltaStart()
        {
            var rung = _peldanos[0];

            Assert.IsFalse(rung.Energizado(_estado), "En reposo no debe haber corriente.");

            _plc.PulsarStart(); _estado.AvisarStart();
            Assert.IsTrue(rung.Energizado(_estado));

            // Pasado el destello, el pulsador ya no conduce: lo que mantiene el peldaño es
            // el contacto de la propia bobina en paralelo. Eso es el enclavamiento.
            _estado.Tick(VdfEstadoLadder.DuracionDestello + 0.1f);
            Assert.IsFalse(_estado.Leer(VdfSenalPlc.PulsadorStart),
                "El destello del pulsador ya debería haber terminado.");
            Assert.IsTrue(rung.Energizado(_estado),
                "Sin el contacto de enclavamiento, el sistema se pararía al soltar START.");
        }

        [Test]
        public void ElContactoDeStop_EsNormalmenteCerrado()
        {
            var rung = _peldanos[0];
            VdfLadderElemento stop = null;
            foreach (var e in rung.Serie) if (e.Etiqueta == "STOP") stop = e;

            Assert.IsNotNull(stop);
            Assert.AreEqual(VdfLadderSimbolo.ContactoNC, stop.Simbolo,
                "STOP se cablea como normalmente cerrado: así para el sistema tanto al " +
                "pulsarlo como si se corta el cable. El diagrama debe enseñarlo tal cual.");
        }

        [Test]
        public void LaSeta_CortaElEnclavamientoYLosIndicadores()
        {
            _plc.PulsarStart(); _estado.AvisarStart();
            _plc.PulsarSubir();
            _plc.Tick(4f);
            Assert.AreEqual(1, _plc.NivelActivo);
            Assert.IsTrue(PorBobina("V1").Energizado(_estado));

            _plc.PulsarEmergencia();

            Assert.IsFalse(_peldanos[0].Energizado(_estado), "La seta corta el enclavamiento.");
            for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
                Assert.IsFalse(PorBobina("V" + n).Energizado(_estado),
                    "Con la seta pulsada no puede quedar ningún indicador encendido.");
        }

        [Test]
        public void LaTablaS5S6_SaleDeLosPeldanosYCoincideConElManual()
        {
            // nivel 1 -> ninguna, 2 -> S5, 3 -> S6, 4 -> ambas.
            bool[,] esperado = { { false, false }, { true, false }, { false, true }, { true, true } };

            _plc.PulsarStart();
            for (int nivel = 1; nivel <= 4; nivel++)
            {
                _plc.PulsarSubir();
                _plc.Tick(4f);
                Assert.AreEqual(nivel, _plc.NivelActivo);

                Assert.AreEqual(esperado[nivel - 1, 0], PorBobina("S5").Energizado(_estado),
                    "S5 en el nivel " + nivel);
                Assert.AreEqual(esperado[nivel - 1, 1], PorBobina("S6").Energizado(_estado),
                    "S6 en el nivel " + nivel);
            }
        }

        [Test]
        public void SoloUnIndicadorEncendidoALaVez()
        {
            _plc.PulsarStart();
            for (int nivel = 1; nivel <= 4; nivel++)
            {
                _plc.PulsarSubir();
                _plc.Tick(4f);

                int encendidos = 0;
                for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
                    if (PorBobina("V" + n).Energizado(_estado)) encendidos++;

                Assert.AreEqual(1, encendidos, "En el nivel " + nivel);
            }
        }

        // =====================================================================================
        // Temporizador y contador
        // =====================================================================================

        [Test]
        public void ElTemporizador_AvanzaDuranteElRetardoYSeApagaAlVencer()
        {
            _plc.PulsarStart();
            _plc.PulsarSubir();

            Assert.AreEqual(0f, _estado.ProgresoTemporizador(), 0.01f,
                "Nada más pedir el cambio, el temporizador arranca en cero.");

            _plc.Tick(1.5f);
            float mitad = _estado.ProgresoTemporizador();
            Assert.Greater(mitad, 0.3f);
            Assert.Less(mitad, 0.7f);

            _plc.Tick(2f);
            Assert.IsFalse(_plc.CambioEnCurso, "El retardo ya debería haber vencido.");
            Assert.AreEqual(0f, _estado.ProgresoTemporizador(), 0.01f);
        }

        /// <summary>
        /// El contador marca el nivel PEDIDO, igual que la cuenta del Ladder original, que
        /// sube en cuanto se pulsa. Lo que espera los 3 s es la salida, no la cuenta.
        /// </summary>
        [Test]
        public void ElContador_MarcaElDestinoYAvisaDeQueEstaTemporizando()
        {
            _plc.PulsarStart();
            Assert.AreEqual("0 / 4", _estado.TextoContador());

            _plc.PulsarSubir();
            Assert.AreEqual("1 / 4  (temporizando)", _estado.TextoContador(),
                "La cuenta ya esta en 1; lo que falta es que venza el temporizador.");

            _plc.Tick(4f);
            Assert.AreEqual("1 / 4", _estado.TextoContador());

            // Y subiendo de nivel con el motor ya girando, igual.
            _plc.PulsarSubir();
            Assert.AreEqual("2 / 4  (temporizando)", _estado.TextoContador());
            Assert.AreEqual(0, _plc.NivelActivo, "mientras tanto el motor esta parado");
        }

        // =====================================================================================
        // Estructura
        // =====================================================================================

        [Test]
        public void HayUnPeldanoPorCadaSalidaDelPlc()
        {
            Assert.IsNotNull(PorBobina("S1"));
            Assert.IsNotNull(PorBobina("S5"));
            Assert.IsNotNull(PorBobina("S6"));
            for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
                Assert.IsNotNull(PorBobina("V" + n), "Falta el peldaño del indicador V" + n);
        }

        [Test]
        public void TodoPeldanoTieneTituloSerieYBobina()
        {
            for (int i = 0; i < _peldanos.Count; i++)
            {
                var p = _peldanos[i];
                Assert.IsFalse(string.IsNullOrEmpty(p.Titulo), "Peldaño " + (i + 1) + " sin título");
                Assert.Greater(p.Serie.Count, 0, "Peldaño " + (i + 1) + " sin contactos");
                Assert.IsNotNull(p.Bobina, "Peldaño " + (i + 1) + " sin salida");
            }
        }

        [Test]
        public void ElDiagramaSeEnteraDeLasPulsaciones_SinQueNadieLeAvisePorSuCuenta()
        {
            // Este test existe por un fallo que los demás no podían ver: todos avisaban al
            // diagrama ellos mismos (_estado.AvisarStart()), así que comprobaban que el
            // destello funciona, no que alguien lo dispare. En la escena real no lo disparaba
            // nadie: los contactos de START, STOP, SUBIR y BAJAR no se encendían nunca.
            //
            // Aquí se recorre el camino de verdad: simulación -> evento -> panel -> estado.
            var raiz = new UnityEngine.GameObject("RaizLadder");
            try
            {
                var sim = raiz.AddComponent<VdfSimulacion>();
                Despertar(sim);

                var panelGo = new UnityEngine.GameObject("Panel");
                panelGo.transform.SetParent(raiz.transform, false);
                var panel = panelGo.AddComponent<VdfLadderPanel>();
                panel.Simulacion = sim;
                Despertar(panel);
                Suscribir(panel);

                var arranque = VdfLadderPrograma.Crear()[0];
                panel.Estado.Plc = sim.Plc;

                Assert.IsFalse(panel.Estado.Leer(VdfSenalPlc.PulsadorStart),
                    "antes de pulsar nada, el contacto de START está apagado");

                sim.PulsarStart();
                Assert.IsTrue(panel.Estado.Leer(VdfSenalPlc.PulsadorStart),
                    "al pulsar START el contacto del diagrama tiene que encenderse");
                Assert.IsTrue(arranque.Energizado(panel.Estado),
                    "y con él, el peldaño del enclavamiento");

                sim.PulsarSubir();
                Assert.IsTrue(panel.Estado.Leer(VdfSenalPlc.PulsadorSubir));

                // El destello se apaga solo, como en un visor de verdad.
                panel.Estado.Tick(VdfEstadoLadder.DuracionDestello + 0.1f);
                Assert.IsFalse(panel.Estado.Leer(VdfSenalPlc.PulsadorSubir),
                    "pasado el destello, el contacto vuelve a apagarse");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(raiz);
            }
        }

        /// <summary>En EditMode, AddComponent no dispara Awake: hay que llamarlo a mano.</summary>
        private static void Despertar(UnityEngine.Component c)
        {
            var awake = c.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(c, null);
        }

        /// <summary>Tampoco dispara OnEnable, que es donde el panel se suscribe.</summary>
        private static void Suscribir(VdfLadderPanel panel)
        {
            var onEnable = typeof(VdfLadderPanel).GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (onEnable != null) onEnable.Invoke(panel, null);
        }

        [Test]
        public void SinPlcConectado_NadaRevienta()
        {
            var vacio = new VdfEstadoLadder(null);

            Assert.DoesNotThrow(delegate()
            {
                foreach (var p in _peldanos) p.Energizado(vacio);
                vacio.TextoContador();
                vacio.TextoTemporizador();
                vacio.ProgresoTemporizador();
            });
        }
    }
}
