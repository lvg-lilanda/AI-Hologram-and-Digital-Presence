# Requirements Document - Sprint 1 Week 1

# 1. Project

## 1.1 Client Context

**Client:** Telstra – muru-D

Telstra is Australia's largest telecommunications company, while muru-D is its innovation and incubation hub for prototyping new technologies. The project is currently understood as an experimental showcase piece rather than a customer-facing product.

## 1.2 The Problem

Muru-D wants a convincing way to "appear" in a room without actually being there, particularly for a real presentation or a quick Q&A. However, the specific business problem, primary user, target audience and use case have not yet been confirmed. At this stage, the technology and overall concept are clearer than the specific user problem the project should address. 

The purpose of Sprint 1 Week 1 is to establish a clear foundation for clarifying the project requirements, including the problem, users, audience and priorities, before researching and evaluating potential use cases.

## 1.3 Project Concept

The project explores the creation of a digital human hologram that can allow a person to appear digitally in a physical space without being physically present.

The project concept involves:

* Cloning the voice of a consenting participant
* Synchronizing the generated/cloned voice with video
* Displaying the resulting digital human on a screen/hologram
* Moving the output onto the real hologram installation
* Potentially extending the concept into an interactive use case such as Q&A

The project brief identifies responsible AI use as a requirement, including the use of a consenting participant and avoiding impersonation of third parties.

## 1.4 Project Context

### 1.4.1 Confirmations

| Area                       | Information                                                            |
| -------------------------- | ---------------------------------------------------------------------- |
| **Core Concept**           | Digital Human Hologram                                                 |
| **Voice**                  | AI-generated/cloned voice                                              |
| **Video**                  | Voice needs to be synchronized with video                              |
| **Display**                | Output must be displayed end-to-end (screen or hologram)               |
| **Hardware**               | Output is intended to eventually run on the real hologram installation |
| **Minimum Viable Product** | Voice, video and display need to work end-to-end                       |
| **Extension**              | At least one extension use case                                        |
| **Consent**                | Voice cloning must involve a consenting participant                    |
| **Ethics**                 | No impersonation of third parties                                      |

### 1.4.2 Assumptions

At this stage, limited assumptions have been made because the primary user, problem and use case have not yet been established.

Current working assumptions are that the primary user may be Telstra – muru-D staff responsible for presenting or operating the experience, while the audience may consist of people attending the presentation or Q&A.

These assumptions will be confirmed or revised following client discussion and research.

### 1.4.3 Key Unknowns

The following areas remain unconfirmed and will be further investigated through client discussions and research:

* Primary user
* Intended audience
* Specific problem
* Use case
* Priorities for Sprint 2

### 1.4.4 Current Decisions

No final use case has been selected at this stage.

The team will not commit to a specific use case until the primary user, problem, client priorities and feasibility have been validated.

Potential use cases identified during research will be evaluated using the selection criteria in Section 2.

## 1.5 Technical Context

### 1.5.1 Devices

* Two devices are available.
* One includes infrared sensors and speakers for haptic feedback.
* The sensors currently detect hands and arms up to the elbows.
* Face tracking may potentially be possible, but this requires further investigation into available libraries.
* The plugins may be updated if the team only uses the sensors.

### 1.5.2 Display

The Looking Glass screen has a particular screen layer that can blur text. Therefore, any UI-heavy use case needs to consider clarity.

### 1.5.3 Development

* The virtual environment requires Unity or Unreal Engine.
* The team will observe other student projects where normal webcams were used for tracking.
* The existing speakers could potentially be retained for sensation if they support the selected use case.
* All Unity projects must target Unity 6000.4.6f1, unless otherwise agreed with the client.
* The project will be managed using Git and stored on the provided Gitea server.
* The Unity Assets, Packages, and ProjectSettings folders should be tracked in Git, while generated folders such as Library should generally not be tracked.
* Git LFS should be used for large binary assets where required.
* The Unity application name and package name must be changed from their default values. The package name should follow the required au.edu.rmit.hudini.computing.AppName.feature format.


# 2. Selection Criteria

These criteria provide a consistent method to compare use cases after research is completed. They are intended to prevent the team from selecting a use case solely because it is technically interesting.

## 2.1 Scorecard Template

| Use Case                                       | Value / 5 | Feasibility / 5 | User Clarity / 5 | Sprint 2 Testability / 5 | Total / 20 | Evidence | Rank |
| ---------------------------------------------- | --------: | --------------: | ---------------: | -----------------------: | ---------: | -------- | ---: |
| Potential use case identified through research |           |                 |                  |                          |            |          |      |


### 2.1.1 Framework

| Criteria                 | What it Measures                                                                                                       |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| **Value**                | How the use case addresses the client objective.                                                                       |
| **Feasibility**          | Whether the use case can realistically be implemented using available technology, resources, hardware and constraints. |
| **User Clarity**         | Whether the intended user and problem are clearly identified and supported by evidence.                                |
| **Sprint 2 Testability** | Whether a meaningful version of the use case can be prototyped, demonstrated and evaluated during Sprint 2.            |

### 2.1.2 Scoring

| Score | Meaning     | Explanation                                          |
| ----: | ----------- | ---------------------------------------------------- |
| **5** | Very Strong | Strong evidence that the criteria are satisfied      |
| **4** | Strong      | Criteria are mostly satisfied with minor uncertainty |
| **3** | Moderate    | Some evidence exists but uncertainty remains         |
| **2** | Weak        | Limited evidence                                     |
| **1** | Very Weak   | Little/no evidence that the criteria are satisfied   |

# 3. Discovery Questions

## A. Understanding the Problem

* What is the specific problem that you want the digital human to solve?
* What is the primary objective?
* What would make the project valuable?

## B. Understanding the User

* Is the primary user expected to be staff, or another user group?
* Who interacts with this directly?
* Who is the intended audience?

## C. Understanding the Use Case

* What type of use case would be most valuable?
* Do we need real-time Q&A or is pre-generated content sufficient?
* How much interaction is expected?
* Are there particular environments where you envision the digital human being used?
* Are there any use cases you would particularly like the team to investigate?
* Are there any use cases or applications that should be excluded?

## D. Understanding Success for Sprint 2

* What should the team demonstrate in Sprint 2?
* Which aspects should be prioritized if time or technical constraints prevent everything from being implemented?
