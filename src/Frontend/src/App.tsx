import { useState } from 'react';
import HealthCheck from './components/HealthCheck';
import './App.css';

function App() {
  const [activeTab, setActiveTab] = useState<'dashboard' | 'reports'>('dashboard');

  return (
    <div className="app">
      <header className="app-header">
        <h1>UPMS - Unified Project Management System</h1>
        <p className="subtitle">Snapshot-Based Reporting & Analysis Platform</p>
      </header>

      <nav className="app-nav">
        <button
          className={`nav-button ${activeTab === 'dashboard' ? 'active' : ''}`}
          onClick={() => setActiveTab('dashboard')}
        >
          Dashboard
        </button>
        <button
          className={`nav-button ${activeTab === 'reports' ? 'active' : ''}`}
          onClick={() => setActiveTab('reports')}
        >
          Reports
        </button>
      </nav>

      <main className="app-content">
        {activeTab === 'dashboard' && (
          <div className="dashboard">
            <h2>System Dashboard</h2>
            <HealthCheck />
            <div className="placeholder-section">
              <h3>Coming Soon</h3>
              <ul>
                <li>Snapshot Management</li>
                <li>Report Generation</li>
                <li>Data Analysis Tools</li>
              </ul>
            </div>
          </div>
        )}

        {activeTab === 'reports' && (
          <div className="reports">
            <h2>Reports</h2>
            <div className="placeholder-section">
              <p>Report generation interface will be available here.</p>
            </div>
          </div>
        )}
      </main>

      <footer className="app-footer">
        <p>UPMS v0.1.0 - Admin UI</p>
      </footer>
    </div>
  );
}

export default App;
