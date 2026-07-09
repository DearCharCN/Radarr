/* eslint-disable react/prop-types */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './CustomFormatMutexGroups.css';

const ENDPOINT = '/customformatmutexgroup';

function requestJson({ url, method = 'GET', data }) {
  const ajaxOptions = {
    url,
    method
  };

  if (method !== 'DELETE') {
    ajaxOptions.dataType = 'json';
  }

  if (data !== undefined) {
    ajaxOptions.contentType = 'application/json';
    ajaxOptions.data = JSON.stringify(data);
  }

  return new Promise((resolve, reject) => {
    const { request } = createAjaxRequest(ajaxOptions);

    request.done((result) => resolve(result));
    request.fail((xhr) => reject(xhr));
  });
}

function getErrorMessage(xhr, fallback) {
  const response = xhr?.responseJSON;

  if (Array.isArray(response)) {
    return response
      .map((item) => item.errorMessage || item.message || item.propertyName)
      .filter(Boolean)
      .join(' ');
  }

  if (response?.errors) {
    return Object.values(response.errors).flat().join(' ');
  }

  return response?.message || response?.error || xhr?.statusText || fallback;
}

function createDefaultCustomFormatMutexGroup() {
  return {
    id: 0,
    name: translate('DefaultCustomFormatMutexGroupName'),
    enabled: true,
    customFormatIds: []
  };
}

function cloneCustomFormatMutexGroup(item) {
  return {
    ...item,
    customFormatIds: [...(item.customFormatIds || [])]
  };
}

function EmptyState({ children }) {
  return <div className={styles.emptyState}>{children}</div>;
}

function CustomFormatMutexGroupForm({ draft, customFormats, setDraft }) {
  const selectedIds = draft.customFormatIds || [];
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const customFormatsById = useMemo(() => {
    return customFormats.reduce((acc, customFormat) => {
      acc[customFormat.id] = customFormat;
      return acc;
    }, {});
  }, [customFormats]);
  const availableCustomFormats = customFormats
    .filter((format) => !selectedIds.includes(format.id));

  function addCustomFormat(id) {
    setDraft((current) => ({
      ...current,
      customFormatIds: [
        ...(current.customFormatIds || []),
        id
      ]
    }));
    setIsPickerOpen(false);
  }

  function removeCustomFormat(id) {
    setDraft((current) => ({
      ...current,
      customFormatIds: (current.customFormatIds || [])
        .filter((customFormatId) => customFormatId !== id)
    }));
  }

  return (
    <>
      <div className={styles.formGrid}>
        <label className={styles.field}>
          <span>{translate('Name')}</span>
          <input
            className={styles.input}
            type="text"
            value={draft.name || ''}
            onChange={(event) => {
              setDraft((current) => ({
                ...current,
                name: event.target.value
              }));
            }}
          />
        </label>
      </div>

      <label className={styles.checkboxField}>
        <input
          type="checkbox"
          checked={!!draft.enabled}
          onChange={(event) => {
            setDraft((current) => ({
              ...current,
              enabled: event.target.checked
            }));
          }}
        />
        <span>{translate('Enabled')}</span>
      </label>

      <div className={styles.arrayHeader}>
        <h3>{translate('CustomFormats')}</h3>
      </div>

      <div className={styles.customFormatMutexGroups}>
        {selectedIds.map((id) => {
          const format = customFormatsById[id];

          if (!format) {
            return null;
          }

          return (
            <Card
              key={id}
              className={styles.selectionCard}
            >
              <div className={styles.nameContainer}>
                <div className={styles.name}>
                  {format.name}
                </div>

                <IconButton
                  className={styles.iconButton}
                  title={translate('Remove')}
                  name={icons.DELETE}
                  onPress={() => removeCustomFormat(id)}
                />
              </div>
            </Card>
          );
        })}

        <Card
          className={styles.addMutexGroup}
          onPress={() => setIsPickerOpen(true)}
        >
          <div className={styles.center}>
            <Icon
              name={icons.ADD}
              size={45}
            />
          </div>
        </Card>
      </div>

      {isPickerOpen ? (
        <div className={styles.pickerPanel}>
          <div className={styles.arrayHeader}>
            <h3>{translate('AvailableCustomFormats')}</h3>
          </div>

          {availableCustomFormats.length ? (
            <div className={styles.customFormatMutexGroups}>
              {availableCustomFormats.map((format) => (
                <Card
                  key={format.id}
                  className={styles.selectionCard}
                  overlayContent={true}
                  onPress={() => addCustomFormat(format.id)}
                >
                  <div className={styles.name}>
                    {format.name}
                  </div>
                </Card>
              ))}
            </div>
          ) : (
            <EmptyState>{translate('NoCustomFormatsAvailable')}</EmptyState>
          )}
        </div>
      ) : null}
    </>
  );
}

function CustomFormatMutexGroupCard({
  item,
  customFormatNames,
  onEditPress,
  onDeletePress
}) {
  const selectedNames = (item.customFormatIds || [])
    .map((id) => customFormatNames[id])
    .filter(Boolean);

  return (
    <Card
      className={styles.mutexGroupCard}
      overlayContent={true}
      onPress={onEditPress}
    >
      <div className={styles.nameContainer}>
        <div className={styles.name}>
          {item.name}
        </div>

        <div className={styles.buttons}>
          <IconButton
            className={styles.iconButton}
            title={translate('Edit')}
            name={icons.EDIT}
            onPress={onEditPress}
          />

          <IconButton
            className={styles.iconButton}
            title={translate('Delete')}
            name={icons.DELETE}
            onPress={onDeletePress}
          />
        </div>
      </div>

      <div className={styles.labels}>
        {
          selectedNames.length ? (
            selectedNames.map((name) => (
              <Label
                key={name}
                className={styles.label}
              >
                {name}
              </Label>
            ))
          ) : (
            <Label className={styles.label}>{translate('None')}</Label>
          )
        }

        {
          item.enabled ? null : (
            <Label className={styles.label} kind={kinds.WARNING}>
              {translate('Disabled')}
            </Label>
          )
        }
      </div>
    </Card>
  );
}

function CustomFormatMutexGroups() {
  const [isFetching, setIsFetching] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [items, setItems] = useState([]);
  const [customFormats, setCustomFormats] = useState([]);
  const [draft, setDraft] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const customFormatNames = useMemo(() => {
    return customFormats.reduce((acc, customFormat) => {
      acc[customFormat.id] = customFormat.name;
      return acc;
    }, {});
  }, [customFormats]);

  const loadData = useCallback(async() => {
    setIsFetching(true);
    setLoadError(null);

    try {
      const [groupItems, customFormatItems] = await Promise.all([
        requestJson({ url: ENDPOINT }),
        requestJson({ url: '/customformat' })
      ]);

      setItems(groupItems || []);
      setCustomFormats(customFormatItems || []);
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('CustomFormatMutexGroupsLoadError')));
    } finally {
      setIsFetching(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openModal = useCallback((item) => {
    setSaveError(null);
    setDraft(
      item ?
        cloneCustomFormatMutexGroup(item) :
        createDefaultCustomFormatMutexGroup()
    );
  }, []);

  const closeModal = useCallback(() => {
    if (isSaving) {
      return;
    }

    setDraft(null);
    setSaveError(null);
  }, [isSaving]);

  const saveDraft = useCallback(async() => {
    if (!draft) {
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    try {
      const id = draft.id;
      const method = id ? 'PUT' : 'POST';
      const url = id ? `${ENDPOINT}/${id}` : ENDPOINT;

      await requestJson({
        url,
        method,
        data: draft
      });

      setDraft(null);
      await loadData();
    } catch (error) {
      setSaveError(getErrorMessage(error, translate('CustomFormatMutexGroupsSaveError')));
    } finally {
      setIsSaving(false);
    }
  }, [draft, loadData]);

  const confirmDelete = useCallback(async() => {
    if (!deleteTarget) {
      return;
    }

    setIsDeleting(true);

    try {
      await requestJson({
        url: `${ENDPOINT}/${deleteTarget.id}`,
        method: 'DELETE'
      });

      setDeleteTarget(null);
      await loadData();
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('CustomFormatMutexGroupsDeleteError')));
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, loadData]);

  return (
    <FieldSet legend={translate('CustomFormatMutexGroups')}>
      {loadError ? <Alert kind="danger">{loadError}</Alert> : null}

      {isFetching ? (
        <LoadingIndicator />
      ) : (
        <div className={styles.customFormatMutexGroups}>
          {items.map((item) => (
            <CustomFormatMutexGroupCard
              key={item.id}
              item={item}
              customFormatNames={customFormatNames}
              onEditPress={() => openModal(item)}
              onDeletePress={() => setDeleteTarget(item)}
            />
          ))}

          <Card
            className={styles.addMutexGroup}
            onPress={() => openModal()}
          >
            <div className={styles.center}>
              <Icon
                name={icons.ADD}
                size={45}
              />
            </div>
          </Card>
        </div>
      )}

      <Modal
        isOpen={!!draft}
        size="large"
        onModalClose={closeModal}
      >
        <ModalContent onModalClose={closeModal}>
          <ModalHeader>
            {
              draft?.id ?
                translate('EditCustomFormatMutexGroup') :
                translate('AddCustomFormatMutexGroup')
            }
          </ModalHeader>

          <ModalBody>
            {saveError ? <Alert kind="danger">{saveError}</Alert> : null}

            {
              draft ? (
                <CustomFormatMutexGroupForm
                  draft={draft}
                  customFormats={customFormats}
                  setDraft={setDraft}
                />
              ) : null
            }
          </ModalBody>

          <ModalFooter>
            <Button onPress={closeModal}>{translate('Cancel')}</Button>

            <SpinnerButton
              kind="primary"
              isSpinning={isSaving}
              onPress={saveDraft}
            >
              {translate('Save')}
            </SpinnerButton>
          </ModalFooter>
        </ModalContent>
      </Modal>

      <ConfirmModal
        isOpen={!!deleteTarget}
        kind={kinds.DANGER}
        title={translate('Delete')}
        message={translate('DeleteCustomFormatMutexGroupMessage', {
          name: deleteTarget?.name || translate('SelectedItem')
        })}
        confirmLabel={translate('Delete')}
        cancelLabel={translate('Cancel')}
        isSpinning={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </FieldSet>
  );
}

export default CustomFormatMutexGroups;
