﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Engine;
using Engine.EntitySystem;
using Engine.MapSystem;
using Engine.MathEx;
using Engine.Utils;
using ProjectCommon;

namespace ProjectEntities
{
    public class ComputerType : DynamicType
    {
    }

    /// <summary>
    /// Main computer that can be controlled by the boss alien and
    /// the astronauts
    /// </summary>
    public class Computer : Dynamic
    {
        /*************/
        /* Attribute */
        /*************/
        ComputerType _type = null; public new ComputerType Type { get { return _type; } }
        const int maxAliens = 20;
        const int maxPowerCoupons = 10;
        const int maxRotationCoupons = 10;

        [FieldSerialize]
        static int experiencePoints = 50;
        static int rotationCoupons = 0;
        static int powerCoupons = 0;
        static int availableAliens = 0;
        static int usedAliens = 0;
        public static bool noSpawnTime = false;

        // Für USB
        static bool alienControlPaused = false;
        static Timer alienControlTimer;

        //Schere Stein PapierWindow
        private static Task csspwTask;
        //Ob Licht geändert werden darf oder nicht
        private static bool allowedToChangeLight;

        

        // Speichert die im Spiel durchgeführten Rotationen, damit das SectorStatusWindow diese beim initialize nachmachen kann
        // ringRotations[0] ist Ring F1, ringRotations[1] ist Ring F2 und ringRotations[2] ist Ring F3 (innerer Ring)
        // Bei Rechtsrotation wird 1 % 8 addiert, bei Linksrotation 1 % 8 subtrahiert.
        static int[] ringRotations = new int[3];

        // Verwaltet alle Alienleichen in der Reihenfolge, wie sie gestorben sind
        static Queue<Corpse> corpseList = new Queue<Corpse>();
        const int maxAlienCorpses = 10;

        // Verwaltet die Signale für das Radar
        public static ThreadSafeList<Signal> signalList = new ThreadSafeList<Signal>();

        // Bis zu welcher Gruppennummer dürfen Items gedroppt werden
        static int maxItemDropGroupNr = 2;

        // Statistik
        static Statistic statistic = new Statistic();

        // Anzeige der Statistik für Server und Clients
        static bool astronautwin = false;
        static bool alienwin = false;
        
        enum NetworkMessages
        {
            StatisticToClient
        }
        public static event StatisticEventDelegate showStatistic;
        public delegate void StatisticEventDelegate();

        /*******************/
        /* Getter / Setter */
        /*******************/
        public static bool Alienwin
        {
            get { return Computer.alienwin; }
            set { Computer.alienwin = value; }
        }

        public static bool Astronautwin
        {
            get { return Computer.astronautwin; }
            set { Computer.astronautwin = value; }
        }

        public static int ExperiencePoints
        {
            get { return experiencePoints; }
            set { experiencePoints = value; }
        }

        public static int RotationCoupons
        {
            get { return rotationCoupons; }
        }

        public static int PowerCoupons
        {
            get { return powerCoupons; }
        }

        public static int AvailableAliens
        {
            get { return availableAliens; }
        }

        public static int UsedAliens
        {
            get { return usedAliens; }
        }

        public static int[] RingRotations
        {
            get { return ringRotations; }
        }

        public static Task CsspwTask
        {
            get { return Computer.csspwTask; }
            set { Computer.csspwTask = value; }
        }

        public static bool AllowedToChangeLight
        {
            get { return Computer.allowedToChangeLight; }
            set { Computer.allowedToChangeLight = value; }
        }

        public static int MaxItemDropGroupNr
        {
            get { return Computer.maxItemDropGroupNr; }
        }

        public static Statistic Statistic
        {
            get { return statistic; }
        }

        /**************/
        /* Funktionen */
        /**************/
        /// <summary>
        /// Anzahl erhöhen bis Gesamtanzahl an Gruppen der Drop-Items des Aliens erreicht wurde. Damit kann eingeschränkt werden, 
        /// bis zu welcher Gruppennummer die Items gedroppt werden dürfen.
        /// </summary>
        public static void IncrementMaxItemDropGroupNr()
        {
            Alien a = Entities.Instance.Create("Alien", Map.Instance) as Alien;
            if (maxItemDropGroupNr < a.Type.DieObjects.Count)
            {
                maxItemDropGroupNr++;
            }
        }

        /// <summary>Light
        /// ExperiencePoints hinzufügen
        /// </summary>
        /// <param name="value"></param>
        public static void AddExperiencePoints(int value)
        {
            if (value >= 0)
            {
                experiencePoints += value;
            }
        }

        /// <summary>
        /// ExperiencePoints um Eins erniedrigen
        /// </summary>
        public static void DecrementExperiencePoints()
        {
            if (experiencePoints > 0)
            {
                experiencePoints--;
            }
        }
        /// <summary>
        /// Rotation Coupons um Eins erhöhen
        /// </summary>
        public static void IncrementRotationCoupons()
        {
            if (rotationCoupons < maxRotationCoupons)
            {
                rotationCoupons++;
            }
        }
        /// <summary>
        /// Rotation Coupons um Eins erniedrigen
        /// </summary>
        public static void DecrementRotationCoupons()
        {
            if (rotationCoupons > 0)
            {
                rotationCoupons--;
            }
        }

        /// <summary>
        /// Power Coupons um Eins erhöhen
        /// </summary>
        public static void IncrementPowerCoupons()
        {
            if (powerCoupons < maxPowerCoupons)
            {
                powerCoupons++;
            }
        }
        /// <summary>
        /// Power Coupons um Eins erniedrigen
        /// </summary>
        public static void DecrementPowerCoupons()
        {
            if (powerCoupons > 0)
            {
                powerCoupons--;
            }
        }

        /// <summary>
        /// Ein Alien wurde gespawnt, d.h. Ein verfügbares Alien weniger und ein aktives Alien mehr
        /// </summary>
        public static void AddUsedAlien()
        {
            if (availableAliens > 0)
            {
                DecrementAvailableAliens();
                IncrementUsedAliens();
            }
        }

        /// <summary>
        /// Verfügbare Aliens um Eins erhöhen
        /// </summary>
        public static void IncrementAvailableAliens()
        {
            if ((availableAliens + usedAliens) < maxAliens)
            {
                availableAliens++;
            }
        }
        /// <summary>
        /// Verfügbare Aliens um Eins erniedrigen
        /// </summary>
        private static void DecrementAvailableAliens()
        {
            availableAliens--;
        }

        /// <summary>
        /// Aktive Aliens um Eins erhöhen
        /// </summary>
        private static void IncrementUsedAliens()
        {
            usedAliens++;
            statistic.IncrementSpawnedAliens();
        }
        /// <summary>
        /// Aktive Aliens um Eins erniedrigen
        /// </summary>
        public static void DecrementUsedAliens()
        {
            if (usedAliens > 0)
            {
                usedAliens--;
                statistic.IncrementKilledAliens();
            }
        }

        /// <summary>
        /// Do one rotation of one ring to the left or to the right side.
        /// </summary>
        /// <param name="ring"></param>
        /// <param name="left"></param>
        public static void RotateRing(Ring ring, bool left)
        {
            if (alienControlPaused)
            {
                StatusMessageHandler.sendMessage("Die Astronauten haben die Kontrolle");
            }
            else if (ring == null)
            {
                // Nachricht ausgeben
                StatusMessageHandler.sendMessage("Kein Ring ausgewählt");
            }
            else if (rotationCoupons <= 0)
            {
                // Nachricht ausgeben
                StatusMessageHandler.sendMessage("Keine Rotationen möglich");
            }
            else if (ring.CanRotate() == false)
            {
                StatusMessageHandler.sendMessage("Dieser Ring ist zur Zeit nicht rotierbar");
            }
            else
            {
                Computer.DecrementRotationCoupons();
                int ringNumber = ring.GetRingNumber();
                if (left)
                {
                    EngineConsole.Instance.Print("computer links drehen");
                    ringRotations[ringNumber - 1] = mod(ringRotations[ringNumber - 1] -1 ,8);
                    ring.RotateLeft();
                }
                else
                {
                    EngineConsole.Instance.Print("computer rechts drehen");
                    ringRotations[ringNumber - 1] = mod(ringRotations[ringNumber - 1] + 1, 8);
                    ring.RotateRight();
                }
                ///ToDo: Gridaktualisierung für die Rotation in der Map
                GridBasedNavigationSystem.Instances[0].UpdateMotionMap();
            }
        }

        public static void AstronautRotateRing(Ring ring, bool left)
        {
            if (ring == null)
            {
                // Nachricht ausgeben
                StatusMessageHandler.sendMessage("Kein Ring ausgewählt");
            }
            else
            {
                int ringNumber = ring.GetRingNumber();
                if (left)
                {
                    EngineConsole.Instance.Print("computer links drehen");
                    ringRotations[ringNumber - 1] = mod(ringRotations[ringNumber - 1] - 1, 8);
                    ring.RotateLeft();
                }
                else
                {
                    EngineConsole.Instance.Print("computer rechts drehen");
                    ringRotations[ringNumber - 1] = mod(ringRotations[ringNumber - 1] + 1, 8);
                    ring.RotateRight();
                }
                ///ToDo: Gridaktualisierung für die Rotation in der Map
                GridBasedNavigationSystem.Instances[0].UpdateMotionMap();
            }
        }

        /// <summary>
        /// Switches off or on the power of one sector (room)
        /// </summary>
        /// <param name="sector"></param>
        public static void SetSectorPower(Sector sector)
        {
            if (alienControlPaused)
            {
                StatusMessageHandler.sendMessage("Die Astronauten haben die Kontrolle");
            }
            else if (allowedToChangeLight)
            {
                if (sector == null)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Sector ausgewählt");
                }
                else if (powerCoupons <= 0)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
                }
                else
                {
                    Computer.DecrementPowerCoupons();
                    sector.SwitchLights(false);
                }
            }
            else
            {
                StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
            }
            
        }

        /// <summary>
        /// Switches on the light of one sectorgroup
        /// </summary>
        /// <param name="sectorGroup"></param>
        /// <param name="b"</param>
        public static void SetSectorGroupPowerAstronaut(SectorGroup sectorGroup, bool b)
        {
            if (alienControlPaused)
            {
                StatusMessageHandler.sendMessage("Die Astronauten haben die Kontrolle");
            }
            else if (allowedToChangeLight)
            {
                if (sectorGroup == null)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Sector ausgewählt");
                }
                else if (powerCoupons <= 0)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
                }
                else
                {
                    sectorGroup.LightStatus = b;
                }
            }
            else
            {
                StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
            }

        }

        /// <summary>
        /// Switches off or on the power of one sectorgroup
        /// </summary>
        /// <param name="sectorGroup"></param>
        /// <param name="b"</param>
        public static void SetSectorGroupPower(SectorGroup sectorGroup, bool b)
        {
            if (alienControlPaused)
            {
                StatusMessageHandler.sendMessage("Die Astronauten haben die Kontrolle");
            }
            else if (allowedToChangeLight)
            {
                if (sectorGroup == null)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Sector ausgewählt");
                }
                else if (powerCoupons <= 0)
                {
                    // Nachricht ausgeben
                    StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
                }
                else
                {
                    Computer.DecrementPowerCoupons();
                    sectorGroup.LightStatus = b;
                }
            }
            else
            {
                StatusMessageHandler.sendMessage("Kein Stromabschalten möglich");
            }
            
        }

        /// <summary>
        /// Liefert die Anzahl Astronauten, die leben
        /// </summary>
        /// <returns></returns>
        public static int GetNumberOfActiveAstronauts()
        {
            IEnumerable<GameCharacter> player = Entities.Instance.EntitiesCollection.OfType<GameCharacter>();
            int counter = player.Count();
            // Falls tote Spieler trotzdem ausgelesen werden prüfen, ob diese noch Lebenspunkte haben oder schon tod sind
            foreach (GameCharacter p in player)
            {
                if (p.IsDestroyed || p.Died || p.Health <= 0)
                {
                    // Dann um Eins dekrementieren
                    counter--;
                }
            }
            // Anzahl zurückgeben
            return counter;
        }

        /// <summary>
        /// Alles wird auf das Maximum gesetzt
        /// </summary>
        public static void SetToMaximum()
        {
            availableAliens = (maxAliens - usedAliens);
            rotationCoupons = maxRotationCoupons;
            powerCoupons = maxPowerCoupons;
        }

        /*public static void doCsspwSet()
        {
            if (csspwSet != null)
                csspwSet();
        }*/

        private static int mod(int x, int m)
        {
            return (x%m + m)%m;
        }

        /// <summary>
        /// Fügt eine neue Alienleiche der Liste hinzu
        /// </summary>
        /// <param name="corpse"></param>
        public static void AddAlienCorpse(Corpse corpse)
        {
            corpseList.Enqueue(corpse);
            if (corpseList.Count > maxAlienCorpses)
            {
                DeleteFirstAlienCorpse();
            }
        }

        /// <summary>
        /// Wenn USB von Astronauten eingesetzt wurde, dann hat das Alien für 2 Min keine Kontrolle.
        /// Timmer erstellen, der nach 2 Min die Kontrolle wieder einstellt.
        /// </summary>
        public static void SetAlienControlPaused()
        {
            alienControlPaused = true;
            StatusMessageHandler.sendControlMessage("Astronauten haben die Kontrolle!");
            alienControlTimer = new Timer(120000); // 2 Min
            alienControlTimer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
            alienControlTimer.Enabled = true; // Enable it
        }

        /// <summary>
        /// Kontrolle für Alien wieder einstellen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            alienControlPaused = false;
            StatusMessageHandler.sendControlMessage("");
        }

        /// <summary>
        /// Löscht eine Alienleiche aus der Liste und lässt die Leiche im Spiel verschwinden
        /// </summary>
        private static void DeleteFirstAlienCorpse()
        {
            Corpse corpse = corpseList.Dequeue();
            corpse.Die();
        }
        
        public static void AddDamageAstronouts(float damage)
        {
            statistic.AddDamageAstronouts(damage);
        }

        public static void IncrementKilledAstronouts()
        {
            statistic.IncrementKilledAstronouts();
        }

        /// <summary>
        /// Fügt ein Signal der Minimap für eine gewisse Zeit hinzu
        /// </summary>
        /// <param name="v"></param>
        public static void AddRadarElement(Vec2 min, Vec2 max)
        {
            //Position an AlienGameWindow senden, um damit weiter zu arbeiten
            Signal s = new Signal(min, max);
            if (!signalList.Contains(s))
            {
                signalList.AddLast(s);

                Timer radarTimer = new Timer(30000);
                radarTimer.Elapsed += new ElapsedEventHandler(RemoveRadarElement);
                radarTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Löscht das erste Signalelement der Signalliste
        /// </summary>
        /// <param name="v"></param>
        static void RemoveRadarElement(object source, ElapsedEventArgs e)
        {
            //Position an AlienGameWindow senden, um damit weiter zu arbeiten
            if (signalList.Count() > 0 && signalList != null) 
            {
                signalList.TryRemoveFirst();
            }
        }

        /// <summary>
        /// Reset aller Attribute, die mit der Zeit erhöht werden, damit bei einem Neustart (ohne Beenden der exe) die alten Werte nicht
        /// einfach weiter hochgezählt werden. Wird beim OnAttach im AlienGameWindow ausgeführt.
        /// </summary>
        public static void Reset()
        {
            experiencePoints = 50;
            rotationCoupons = 0;
            powerCoupons = 0;
            availableAliens = 0;
            usedAliens = 0;
            maxItemDropGroupNr = 2;
        }

        public static void SetWinner(bool alienWin)
        {
            alienwin = alienWin;
            astronautwin = !alienWin;

            // Clients bescheid geben
            //Computer c = new Computer();
            //c.Server_SendStatisticToClients();
            // Event feuern, damit die Statistik angezeigt wird
            //if(showStatistic != null)
            //{
            //    showStatistic();
            //}
        }

        private void Server_SendStatisticToClients()
        {
            SendDataWriter writer = BeginNetworkMessage(typeof(Statistic), (ushort)NetworkMessages.StatisticToClient);
            writer.Write(alienwin);
            EndNetworkMessage();
        }

        [NetworkReceive(NetworkDirections.ToClient, (ushort)NetworkMessages.StatisticToClient)]
        void Client_ReceiveStatistic(RemoteEntityWorld sender, ReceiveDataReader reader)
        {
            bool win = reader.ReadBoolean();
            if (!reader.Complete())
                return;

            alienwin = win;
            astronautwin = !win;
            // CaveWindow per Event mitteilen, dass die Statistik angezeigt werden soll.
            if (showStatistic != null)
            {
                showStatistic();
            }
        }

    }
}