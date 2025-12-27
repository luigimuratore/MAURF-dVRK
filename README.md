
# MAURF-dVRK: Multi-Agent Unified Reinforcement Framework for da Vinci Research Kit

<div align="center">
  <img src="assets/images/surgicalScene.png" alt="MAURF-dVRK" width="300"/>
</div>

<div align="center">


<div class="authors">
  <span class="author-block">
    <a href="#">Luigi Muratore</a><sup>1,2</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.com/citations?user=nnDCLvkAAAAJ&hl=en&oi=ao">Federica Barontini</a><sup>2</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.com/citations?user=SZOxkAsAAAAJ&hl=en&oi=ao">Francesco Marzola</a><sup>2</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.com/citations?user=Nmre5_gAAAAJ&hl=en&oi=ao">Matteo Pescio</a><sup>2,3</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.com/citations?user=nnDCLvkAAAAJ&hl=en&oi=ao">Alberto Arezzo</a><sup>2</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.it/citations?user=jfu9BFkAAAAJ&hl=en&oi=ao">Giulio Dagnino</a><sup>2,5</sup>,
  </span>
  <span class="author-block">
    <a href="https://scholar.google.com/citations?user=i4rm0tYAAAAJ&hl=en&oi=ao">Giuseppe Averta</a><sup>4</sup>
  </span>
</div><br>


<div class="affiliations">
  <span class="affiliation-block"><sup>1</sup>DET, Politecnico di Torino, Corso Duca degli Abruzzi 24, Turin, 10129, TO, Italy.</span><br>
  <span class="affiliation-block"><sup>2</sup>Department of Surgical Sciences, Università degli Studi di Torino, Corso Dogliotti 14, Turin, 10126, TO, Italy.</span><br>
  <span class="affiliation-block"><sup>3</sup>DIMEAS, Politecnico di Torino, Corso Duca degli Abruzzi 24, Turin, 10129, TO, Italy.</span><br>
  <span class="affiliation-block"><sup>4</sup>DAUIN, Politecnico di Torino, Corso Duca degli Abruzzi 24, Turin, 10129, TO, Italy.</span><br>
  <span class="affiliation-block"><sup>5</sup>Robotics and Mechatronics, University of Twente, Drienerlolaan 5, Enschede, 7522, NB, Netherlands.</span><br>
</div><br><br>

[![Paper](https://img.shields.io/badge/Paper-PDF-red?style=for-the-badge&logo=adobeacrobatreader)](#)
[![Documentation](https://img.shields.io/badge/Documentation-blue?style=for-the-badge&logo=readthedocs&logoColor=white)](#)
[![Website](https://img.shields.io/badge/Website-green?style=for-the-badge&logo=google-chrome&logoColor=white)](https://luigimuratore.github.io/MAURF-dVRK/)

</div>

---

## 📋 Abstract

**Purpose:** Reliable automation of surgical suturing requires accurate and flexible simulation tools that bridge learning and clinical practice. This work presents a unified Unity–ROS simulation framework for autonomous surgical robotics with the da Vinci Research Kit (dVRK). The framework supports reinforcement-learning-based training of surgical tasks such as needle grasping and placement, while also enabling interactive teleoperation and surgical training. Real-time communication with the dVRK master console allows surgeon control of simulated patient-side manipulators (PSMs), and an integrated graphical user interface (GUI) supports environment tuning for reward design, algorithm development, and sim-to-real validation.

**Methods:** A physics-based surgical environment was developed in Unity, incorporating a full kinematic reconstruction of the dVRK to ensure accurate joint-level and end-effector behavior. Bidirectional ROS communication enables real-time control, state streaming, and visualization between the simulation and the physical master console. Proximal Policy Optimization (PPO) was used for policy training, combined with curriculum learning to progressively increase task difficulty. Domain randomization was applied to improve robustness and generalization. In addition, a multi-agent reinforcement learning strategy is under investigation to enhance cooperative performance in complex tasks such as suturing.

**Results:** Preliminary simulations demonstrated stable policy convergence and precise execution of autonomous needle manipulation tasks. Policies trained with domain randomization showed increased robustness to visual and positional variations, achieving consistent performance across randomized conditions and indicating improved generalization.

**Conclusions:** The proposed Unity–ROS–dVRK framework provides a modular, high-fidelity platform for developing and benchmarking autonomous surgical skills. By integrating accurate kinematics, reinforcement learning, and teleoperation, it supports both autonomous training and human-in-the-loop control in realistic surgical scenarios. Direct ROS connectivity and an integrated GUI further facilitate rapid development and sim-to-real experimentation.

**Keywords:** da Vinci Research Kit (dVRK); surgical robotics; reinforcement learning; Unity; ROS; multi-agent systems; autonomous surgery; simulation.

---

## Key Features

- 🤖 **Multi-agent coordination and collaboration**
- 🎯 **Unified reinforcement learning framework**
- 🖥️ **Graphics User Interface (GUI)**
- 🔗 **ROS integration**
- 🏥 **Integration with da Vinci Research Kit**

---

## 🎬 Demo Videos

For interactive demos, visit our [**project website**](https://luigimuratore.github.io/MAURF-dVRK/).

### Single Agent Tasks

<table>
  <tr>
    <td align="center"><b>Reaching Task</b></td>
    <td align="center"><b>Placement Task</b></td>
    <td align="center"><b>Complete Task</b></td>
  </tr>
  <tr>
    <td align="center"><i>Seed 42, 11, 2026</i></td>
    <td align="center"><i>Seed 42, 11, 2026</i></td>
    <td align="center"><i>Seed 42, 11, 2026</i></td>
  </tr>
</table>

### Multi-Agent Coordination

Collaborative multi-agent demonstrations showing coordinated surgical task execution.

---

## 🏗️ System Architecture

<table>
  <tr>
    <td align="center">
      <img src="assets/images/layer.png" alt="System Architecture" width="300"/>
      <p><i>System Architecture Overview</i></p>
    </td>
    <td align="center">
      <img src="assets/images/multiagent.png" alt="Multi-Agent Coordination" width="600"/>
      <p><i>Multi-Agent Coordination Framework</i></p>
    </td>
  </tr>
</table>

---

## 🤖 Kinematic Model of PSM

Development progression of the Patient Side Manipulator (PSM) kinematic model with constraint implementation.

<div align="center">
  <img src="assets/videos/kinematic/kinematic3.gif" alt="kinematic model" width="400"/>
  <p><i>Kinematic model of the PSM</i></p>
</div>

---

## 📊 Training Results

<div align="center">
  <img src="assets/images/plots/plot1.png" alt="Reaching Task" width="30%"/>
  <img src="assets/images/plots/plot2.png" alt="Placement Task" width="30%"/>
  <img src="assets/images/plots/plot3.png" alt="Complete Task" width="30%"/>
  <p><i>Training convergence for different tasks</i></p>
</div>

---

## 🚀 Getting Started

### Prerequisites

- Unity 2021.3+
- ROS Noetic
- Python 3.8+
- dVRK hardware (optional for sim-to-real)

### Installation

```bash
# Clone the repository
git clone https://github.com/luigimuratore/MAURF-dVRK.git
cd MAURF-dVRK

# Install Python dependencies
pip install -r requirements.txt

# Setup ROS workspace
# Follow instructions in SETUP.md
```

### Running the Simulation

```bash
# Launch the Unity simulation
# (Instructions in documentation)

# Start ROS nodes
...

# Train a policy
mlagent ...
```

For detailed setup instructions, see [SETUP.md](SETUP.md).

---

---

## 📚 Citation

If you use this work in your research, please cite:

```bibtex
@article{muratore2025maurf,
    title={MAURF-dVRK: Multi-Agent Unified Reinforcement Framework for da Vinci Research Kit},
    author={Luigi Muratore and Federica Barontini and Giuseppe Averta},
    journal={Conference/Journal Name},
    year={2026}
}
```

---

## 📧 Contact

For questions or collaboration opportunities, please reach out:

- **Email:** gigiomuratore@gmail.com
- **GitHub:** [@luigimuratore](https://github.com/luigimuratore)

---

<div align="center">
  <p>© 2025 MAURF-dVRK Project</p>
</div>

