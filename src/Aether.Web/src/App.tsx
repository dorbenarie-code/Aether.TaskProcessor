import { JobsList } from "./features/jobs/JobsList";
import { SubmitJobForm } from "./features/jobs/SubmitJobForm";
import "./App.css";

function App() {
  return (
    <main>
      <h1>Aether Task Processor</h1>
      <SubmitJobForm />
      <JobsList />
    </main>
  );
}

export default App;
