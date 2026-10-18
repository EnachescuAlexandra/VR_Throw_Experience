# IMR_Lab2_VR_Throw_Experience
### Enăchescu Alexandra-Maria 3E1

Experiență VR realizată în Unity, în care utilizatorul poate apuca și arunca un cuțit către o țintă, obținând puncte în funcție de distanța față de centrul acesteia.

Update – Lab 3: Blender Hand Model & Animations

Ce a fost realizat conform cerintei:

-Modelarea în Blender a unei mâini 3D cu 5 degete.
-Realizarea UV Unwrap și aplicarea unei texturi.
-Exportarea modelului în format FBX și importarea în Unity.
-Atașarea modelului la controllerele VR pentru ambele mâini (stânga și dreapta, prin oglindire pe axa X).

Bonus – Animații

-Crearea unui schelet și a două animații în Blender:

-Grab: îndoirea degetelor mijlociu, inelar și mic.

-Trigger: îndoirea arătătorului.

-Importarea animațiilor în Unity și asocierea mișcărilor cu acțiunile Grip/Select și Trigger/Activate ale controllerelor VR.

-Testarea ambelor mâini cu XR Device Simulator.

Demo Lab 3: hands_lab3_demo.mp4

### 1. Implementat (vizibil în video atat pe XR simulator cat si pe Meta Quest)
- Configurarea **OpenXR + XR Interaction Toolkit**.
- Utilizarea **XR Device Simulator** pentru testare fără headset (a se vedea XRDevice_demo.mp4).
- Interacțiuni **Grab/Select** pentru apucarea și aruncarea cuțitului, folosind XR Grab Interactable.
- Detectarea coliziunii cu ținta și calcularea scorului în funcție de distanța față de centru.
- Afișarea scorului total printr-un element UI (TextMesh Pro).
- Feedback vizual la impactul cu ținta (culoare verde pentru 3 secunde).
- Țintă împărțită în **3 zone colorate**, cu punctaje diferite: 50p (centru), 25p (mijloc), 10p (exterior).

### 2. Pentru bonus
- Prevenirea punctării multiple pentru aceeași aruncare, inclusiv în cazul ricoșeurilor.
- **Resetarea automată a cuțitului** la poziția inițială după 5 secunde de la eliberare, pentru a putea continua jocul.
- Mediu 3D cu masă, suport pentru țintă (modelata direct in Unity) și pereți.

### 3. Testare suplimentară/ bonus pe Meta Quest 2
- Aplicația a fost compilată și testată cu succes pe un headset **Meta Quest 2** (a se vedea MetaQuest2_demo.mp4)
- Au fost verificate interacțiunile Grab/Throw, punctajul și resetarea automată.
- `XRDevice_demo.mp4` – demonstrație în Unity, folosind XR Device Simulator.
- `MetaQuest2_demo.mp4` – demonstrație pe headsetul Meta Quest 2.

### 4. Organizare
- **Scripturi:** `Assets/Scripts/DartTarget.cs` (scor și feedback vizual), `Assets/Scripts/KnifeReset.cs` (resetarea cuțitului).
- **Scenă:** `Assets/Scenes/SampleScene.unity`.
- **Model 3D:** Cuțit din pachetul PurePoly Free Fantasy RPG Weapons.
