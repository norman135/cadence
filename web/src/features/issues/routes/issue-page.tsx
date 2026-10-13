import { useParams } from 'react-router';
import { IssueDetail } from '../components/issue-detail';

/** An issue on its own page (`/:org/issues/ATL-142`), the shareable link. */
export function IssuePage() {
  const { issueKey } = useParams();
  return (
    <div className="mx-auto max-w-6xl">
      <IssueDetail issueKey={issueKey ?? ''} />
    </div>
  );
}
